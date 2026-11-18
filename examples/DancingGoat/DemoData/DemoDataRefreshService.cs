using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CMS.Base;
using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.ContentWorkflowEngine;
using CMS.ContentWorkflowEngine.Internal;
using CMS.DataEngine;
using CMS.Helpers;
using CMS.Websites;

using Microsoft.Extensions.Logging;

namespace DancingGoat.DemoData;

/// <summary>
/// Refreshes DancingGoat content states and dates so the publishing calendar has current, varied data:
/// sets states through the content APIs (<see cref="DemoDataPlanner"/> decides them), then backdates the
/// last-modified dates with SQL. Runtime state only: Continuous Integration serialization and content
/// versioning are suspended, so <c>App_Data/CIRepository</c> and the version history do not change.
/// </summary>
/// <remarks>
/// Dates are server local time (Xperience stores date/time values in the server's time zone), relative to the
/// server's local date.
/// </remarks>
public sealed class DemoDataRefreshService
{
    private const string ADMINISTRATOR_USER_NAME = "administrator";

    private static readonly SemaphoreSlim refreshLock = new(1, 1);

    private readonly IContentItemManagerFactory contentItemManagerFactory;
    private readonly IWebPageManagerFactory webPageManagerFactory;
    private readonly IContentItemWorkflowManagerFactory contentItemWorkflowManagerFactory;
    private readonly IWebPageWorkflowManagerFactory webPageWorkflowManagerFactory;
    private readonly ILogger<DemoDataRefreshService> logger;


    public DemoDataRefreshService(
        IContentItemManagerFactory contentItemManagerFactory,
        IWebPageManagerFactory webPageManagerFactory,
        IContentItemWorkflowManagerFactory contentItemWorkflowManagerFactory,
        IWebPageWorkflowManagerFactory webPageWorkflowManagerFactory,
        ILogger<DemoDataRefreshService> logger)
    {
        this.contentItemManagerFactory = contentItemManagerFactory;
        this.webPageManagerFactory = webPageManagerFactory;
        this.contentItemWorkflowManagerFactory = contentItemWorkflowManagerFactory;
        this.webPageWorkflowManagerFactory = webPageWorkflowManagerFactory;
        this.logger = logger;
    }


    /// <summary>
    /// Runs the refresh. Concurrent calls wait for the running one.
    /// </summary>
    /// <param name="today">Date the layout is relative to; the server's local date by default. Never earlier, so schedules stay in the future.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DemoDataRefreshSummary> Refresh(DateTime? today, CancellationToken cancellationToken)
    {
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            var now = DateTime.Now;
            var layoutDay = today?.Date ?? now.Date;
            if (layoutDay < now.Date)
            {
                throw new ArgumentOutOfRangeException(nameof(today), "The layout day cannot be in the past.");
            }
            int userId = await GetAdministratorUserId(cancellationToken);
            var workflowSteps = await LoadWorkflowSteps(cancellationToken);
            var plan = DemoDataPlanner.Plan(await LoadVariants(cancellationToken), workflowSteps, layoutDay, now);

            int changed = 0;
            using (new CMSActionContext { ContinuousIntegrationAllowObjectSerialization = false, SendEmails = false })
            using (new ContentItemVersionActionContext { EnableStoreVersion = false })
            {
                foreach (var assignment in plan)
                {
                    if (await Apply(assignment, workflowSteps, userId, cancellationToken))
                    {
                        changed++;
                    }
                }

                await Backdate(plan, userId, cancellationToken);
            }

            ProviderHelper.ClearHashtables(ContentItemLanguageMetadataInfo.OBJECT_TYPE, true);
            CacheHelper.ClearCache();

            var summary = CreateSummary(plan, await LoadVariants(cancellationToken), layoutDay, changed);
            logger.LogInformation("Demo data refreshed: {Variants} variants, {Changed} changed.", summary.Variants, summary.Changed);

            return summary;
        }
        finally
        {
            refreshLock.Release();
        }
    }


    private async Task<bool> Apply(DemoDataAssignment assignment, IReadOnlyDictionary<string, IReadOnlyList<string>> workflowSteps, int userId, CancellationToken cancellationToken)
    {
        var variant = assignment.Variant;
        if (assignment.State == DemoDataState.Unchanged || IsInState(variant, assignment))
        {
            return false;
        }

        var manager = new DemoDataVariantManager(variant, userId, contentItemManagerFactory, webPageManagerFactory,
            contentItemWorkflowManagerFactory, webPageWorkflowManagerFactory);

        // Reset to plain published
        if (variant.ScheduledPublishWhen.HasValue)
        {
            await manager.CancelScheduledPublish(cancellationToken);
        }
        var metadata = await manager.GetMetadata(cancellationToken);
        if (metadata.ScheduledUnpublishWhen.HasValue)
        {
            await manager.CancelScheduledUnpublish(cancellationToken);
        }
        var status = metadata.LatestVersionStatus;
        if (status == VersionStatus.Unpublished)
        {
            await manager.CreateDraft(cancellationToken);
            status = VersionStatus.Draft;
        }
        if (status != VersionStatus.Published)
        {
            await manager.Publish(cancellationToken);
        }

        switch (assignment.State)
        {
            case DemoDataState.Draft:
                await manager.CreateDraft(cancellationToken);
                break;
            case DemoDataState.InWorkflow:
                await manager.CreateDraft(cancellationToken);
                // Step by step, as editors move items
                foreach (string step in workflowSteps[variant.WorkflowName])
                {
                    await manager.MoveToStep(step, cancellationToken);
                    if (string.Equals(step, assignment.WorkflowStepName, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                }
                break;
            case DemoDataState.Unpublished:
                await manager.Unpublish(cancellationToken);
                break;
            case DemoDataState.ScheduledPublish:
                await manager.CreateDraft(cancellationToken);
                await manager.SchedulePublish(assignment.ScheduledWhen!.Value, cancellationToken);
                break;
            case DemoDataState.ScheduledUnpublish:
                await manager.ScheduleUnpublish(assignment.ScheduledWhen!.Value, cancellationToken);
                break;
        }

        return true;
    }


    private static bool IsInState(DemoDataVariant variant, DemoDataAssignment assignment)
    {
        bool noSchedules = !variant.ScheduledPublishWhen.HasValue && !variant.ScheduledUnpublishWhen.HasValue;

        return assignment.State switch
        {
            DemoDataState.Published => noSchedules && variant.LatestVersionStatus == VersionStatus.Published,
            DemoDataState.Draft => noSchedules && variant.LatestVersionStatus == VersionStatus.Draft
                && variant.WorkflowStepType is null or ContentWorkflowStepType.SystemDraft,
            DemoDataState.InWorkflow => noSchedules && variant.LatestVersionStatus == VersionStatus.Draft
                && string.Equals(variant.WorkflowStepName, assignment.WorkflowStepName, StringComparison.OrdinalIgnoreCase),
            DemoDataState.Unpublished => noSchedules && variant.LatestVersionStatus == VersionStatus.Unpublished,
            DemoDataState.ScheduledPublish => variant.ScheduledPublishWhen == assignment.ScheduledWhen && !variant.ScheduledUnpublishWhen.HasValue,
            DemoDataState.ScheduledUnpublish => variant.LatestVersionStatus == VersionStatus.Published
                && !variant.ScheduledPublishWhen.HasValue && variant.ScheduledUnpublishWhen == assignment.ScheduledWhen,
            _ => true,
        };
    }


    /// <summary>The content APIs stamp "now"; the calendar's activity date is the last modification, so set it per plan.</summary>
    private static async Task Backdate(IEnumerable<DemoDataAssignment> plan, int userId, CancellationToken cancellationToken)
    {
        const string query = """
            UPDATE CMS_ContentItemLanguageMetadata
            SET ContentItemLanguageMetadataModifiedWhen = @ModifiedWhen,
                ContentItemLanguageMetadataModifiedByUserID = @UserID,
                ContentItemLanguageMetadataCreatedWhen = CASE
                    WHEN ContentItemLanguageMetadataCreatedWhen > @ModifiedWhen THEN @ModifiedWhen
                    ELSE ContentItemLanguageMetadataCreatedWhen
                END
            WHERE ContentItemLanguageMetadataID = @MetadataID
            """;

        foreach (var assignment in plan)
        {
            var parameters = new QueryDataParameters
            {
                { "@ModifiedWhen", assignment.ModifiedWhen },
                { "@UserID", userId },
                { "@MetadataID", assignment.Variant.MetadataId },
            };
            await ConnectionHelper.ExecuteNonQueryAsync(query, parameters, QueryTypeEnum.SQLQuery, cancellationToken);
        }
    }


    private static DemoDataRefreshSummary CreateSummary(IReadOnlyList<DemoDataAssignment> plan, IReadOnlyList<DemoDataVariant> variants, DateTime today, int changed)
    {
        var statuses = variants
            .GroupBy(v => v.CalendarStatus)
            .ToDictionary(g => g.Key, g => g.Count());
        statuses["scheduled-unpublish"] = variants.Count(v => v.ScheduledUnpublishWhen.HasValue);

        var workflowSteps = variants
            .Where(v => v.WorkflowStepType == ContentWorkflowStepType.Custom && !v.ScheduledPublishWhen.HasValue)
            .GroupBy(v => $"{v.WorkflowName} / {v.WorkflowStepName}")
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count());

        var dates = plan.Select(a => a.ModifiedWhen).Concat(plan.Where(a => a.ScheduledWhen.HasValue).Select(a => a.ScheduledWhen!.Value)).ToList();
        var busyDay = today.AddDays(DemoDataPlanner.BUSY_DAY_OFFSET);
        var yesterday = today.AddDays(-1);

        return new(
            FormatDay(today),
            FormatDay(dates.Min()),
            FormatDay(dates.Max()),
            plan.Count(a => a.ModifiedWhen.Date == today),
            plan.Count(a => a.ModifiedWhen.Date == yesterday),
            FormatDay(busyDay),
            plan.Count(a => a.State == DemoDataState.Published && a.ModifiedWhen.Date == busyDay),
            plan.Count,
            changed,
            statuses.OrderBy(s => s.Key, StringComparer.Ordinal).ToDictionary(s => s.Key, s => s.Value),
            workflowSteps);
    }


    private static string FormatDay(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);


    private static async Task<int> GetAdministratorUserId(CancellationToken cancellationToken)
    {
        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            "SELECT UserID FROM CMS_User WHERE UserName = @UserName",
            new QueryDataParameters { { "@UserName", ADMINISTRATOR_USER_NAME } },
            QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"User '{ADMINISTRATOR_USER_NAME}' not found.");
        }

        return reader.GetInt32("UserID");
    }


    /// <summary>Custom steps of every workflow (code names), in step order.</summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadWorkflowSteps(CancellationToken cancellationToken)
    {
        const string query = """
            SELECT w.ContentWorkflowName, s.ContentWorkflowStepName
            FROM CMS_ContentWorkflow w
            INNER JOIN CMS_ContentWorkflowStep s
                ON s.ContentWorkflowStepWorkflowID = w.ContentWorkflowID
            WHERE s.ContentWorkflowStepType = @CustomStep
            ORDER BY w.ContentWorkflowName, s.ContentWorkflowStepOrder
            """;

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(
            query,
            new QueryDataParameters { { "@CustomStep", (int)ContentWorkflowStepType.Custom } },
            QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var steps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            string workflowName = reader.GetString("ContentWorkflowName");
            if (!steps.TryGetValue(workflowName, out var list))
            {
                steps[workflowName] = list = [];
            }
            list.Add(reader.GetString("ContentWorkflowStepName"));
        }

        return steps.ToDictionary(s => s.Key, s => (IReadOnlyList<string>)s.Value, StringComparer.OrdinalIgnoreCase);
    }


    /// <summary>Language variants of pages, reusable items, and emails.</summary>
    private static async Task<IReadOnlyList<DemoDataVariant>> LoadVariants(CancellationToken cancellationToken)
    {
        const string query = """
            SELECT
                lm.ContentItemLanguageMetadataID,
                ci.ContentItemID,
                ci.ContentItemGUID,
                cl.ContentLanguageName,
                c.ClassName,
                c.ClassContentTypeType,
                lm.ContentItemLanguageMetadataLatestVersionStatus,
                ws.ContentWorkflowStepName,
                ws.ContentWorkflowStepType,
                lm.ContentItemLanguageMetadataScheduledPublishWhen,
                lm.ContentItemLanguageMetadataScheduledUnpublishWhen,
                wp.WebPageItemID,
                wp.WebPageItemWebsiteChannelID,
                w.ContentWorkflowName
            FROM CMS_ContentItemLanguageMetadata lm
            INNER JOIN CMS_ContentItem ci
                ON ci.ContentItemID = lm.ContentItemLanguageMetadataContentItemID
            INNER JOIN CMS_Class c
                ON c.ClassID = ci.ContentItemContentTypeID
            INNER JOIN CMS_ContentLanguage cl
                ON cl.ContentLanguageID = lm.ContentItemLanguageMetadataContentLanguageID
            LEFT JOIN CMS_ContentWorkflowStep ws
                ON ws.ContentWorkflowStepID = lm.ContentItemLanguageMetadataContentWorkflowStepID
            LEFT JOIN CMS_WebPageItem wp
                ON wp.WebPageItemContentItemID = ci.ContentItemID
            LEFT JOIN CMS_ContentWorkflowContentType wct
                ON wct.ContentWorkflowContentTypeContentTypeID = c.ClassID
            LEFT JOIN CMS_ContentWorkflow w
                ON w.ContentWorkflowID = wct.ContentWorkflowContentTypeContentWorkflowID
            WHERE c.ClassContentTypeType IN ('Website', 'Reusable', 'Email')
            ORDER BY lm.ContentItemLanguageMetadataID
            """;

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(query, [], QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var variants = new List<DemoDataVariant>();
        while (await reader.ReadAsync(cancellationToken))
        {
            variants.Add(new(
                reader.GetInt32("ContentItemLanguageMetadataID"),
                reader.GetInt32("ContentItemID"),
                reader.GetGuid("ContentItemGUID"),
                reader.GetString("ContentLanguageName"),
                reader.GetString("ClassName"),
                reader.GetString("ClassContentTypeType"),
                (VersionStatus)reader.GetInt32("ContentItemLanguageMetadataLatestVersionStatus"),
                reader.IsDBNull("ContentWorkflowStepName") ? "" : reader.GetString("ContentWorkflowStepName"),
                reader.IsDBNull("ContentWorkflowStepType") ? null : (ContentWorkflowStepType)reader.GetInt32("ContentWorkflowStepType"),
                reader.IsDBNull("ContentItemLanguageMetadataScheduledPublishWhen") ? null : reader.GetDateTime("ContentItemLanguageMetadataScheduledPublishWhen"),
                reader.IsDBNull("ContentItemLanguageMetadataScheduledUnpublishWhen") ? null : reader.GetDateTime("ContentItemLanguageMetadataScheduledUnpublishWhen"),
                reader.IsDBNull("WebPageItemID") ? 0 : reader.GetInt32("WebPageItemID"),
                reader.IsDBNull("WebPageItemWebsiteChannelID") ? 0 : reader.GetInt32("WebPageItemWebsiteChannelID"),
                reader.IsDBNull("ContentWorkflowName") ? "" : reader.GetString("ContentWorkflowName")));
        }

        return variants;
    }
}
