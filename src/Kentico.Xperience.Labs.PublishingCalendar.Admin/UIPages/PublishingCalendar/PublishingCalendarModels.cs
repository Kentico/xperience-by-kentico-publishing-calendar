using Kentico.Xperience.Admin.Base;

namespace Kentico.Xperience.Labs.PublishingCalendar.Admin;

internal class PublishingCalendarPageClientProperties : TemplateClientProperties
{
    public string LoadCalendarCommandName { get; set; } = "";
    public string LoadDayItemsCommandName { get; set; } = "";
    public int DayItemsPageSize { get; set; }
    public IReadOnlyList<PublishingCalendarContentTypeOption> ContentTypes { get; set; } = [];
    public IReadOnlyList<PublishingCalendarChannelOption> Channels { get; set; } = [];
    public IReadOnlyList<PublishingCalendarLanguageOption> Languages { get; set; } = [];
    public IReadOnlyList<PublishingCalendarWorkflowOption> Workflows { get; set; } = [];
}

/// <summary>
/// A calendar day in the browser's time zone. <paramref name="Key"/> is the client's yyyy-mm-dd day key,
/// <paramref name="Start"/>/<paramref name="End"/> are the day's instant boundaries (end exclusive).
/// </summary>
internal record PublishingCalendarDay(string Key, DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Content type / channel / workflow step / language filters. OR within a filter, AND across filters. Empty = no filtering.
/// Channel ID <see cref="PublishingCalendarChannelOption.CONTENT_HUB_CHANNEL_ID"/> matches reusable (channel-less) items.
/// Workflow step IDs match variants currently in that step. Language IDs match the variant's content language.
/// </summary>
internal record PublishingCalendarFilters(int[]? ContentTypeIds, int[]? ChannelIds, int[]? WorkflowStepIds, int[]? LanguageIds = null);

internal record LoadCalendarParams(PublishingCalendarDay[] Days, PublishingCalendarFilters? Filters);

internal record LoadDayItemsParams(PublishingCalendarDay Day, string Status, int Skip, int Take, PublishingCalendarFilters? Filters);

/// <summary>
/// Accurate per day/status counts plus item rows capped per day/status.
/// </summary>
internal record PublishingCalendarResult(IReadOnlyList<PublishingCalendarDayStatusCount> Counts, IReadOnlyList<PublishingCalendarEvent> Items);

internal record PublishingCalendarDayStatusCount(string Day, string Status, int Count);

internal record PublishingCalendarDayItemsResult(int Total, IReadOnlyList<PublishingCalendarEvent> Items);

internal record PublishingCalendarContentTypeOption(int Id, string Name, string Icon);

internal record PublishingCalendarChannelOption(int Id, string Name, string ChannelType)
{
    /// <summary>Pseudo channel for reusable content hub items (no channel).</summary>
    public const int CONTENT_HUB_CHANNEL_ID = 0;
}

/// <summary>A content language. <c>Icon</c> is the language's flag icon name (may be empty).</summary>
internal record PublishingCalendarLanguageOption(int Id, string Name, string DisplayName, string Icon, bool IsDefault);

/// <summary>A workflow and its steps in step order.</summary>
internal record PublishingCalendarWorkflowOption(int Id, string Name, IReadOnlyList<PublishingCalendarWorkflowStepOption> Steps);

/// <summary>A workflow step. <c>Count</c> is the number of language variants currently in the step.</summary>
internal record PublishingCalendarWorkflowStepOption(int Id, string Name, string Icon, int Count);

/// <summary>
/// Calendar entry kinds, shared with the client (publishing-calendar status keys).
/// </summary>
internal static class PublishingCalendarEventStatus
{
    public const string PUBLISHED = "published";
    public const string SCHEDULED_PUBLISH = "scheduled-publish";
    public const string SCHEDULED_UNPUBLISH = "scheduled-unpublish";
    public const string IN_WORKFLOW = "in-workflow";
    public const string DRAFT = "draft";
    public const string UNPUBLISHED = "unpublished";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        PUBLISHED, SCHEDULED_PUBLISH, SCHEDULED_UNPUBLISH, IN_WORKFLOW, DRAFT, UNPUBLISHED
    };
}

/// <summary>
/// A single publishing calendar entry for a content item language variant.
/// </summary>
/// <remarks>
/// <c>Id</c> is unique per language variant and status kind. <c>Day</c> is the client day key the entry falls on.
/// <c>Date</c> is the instant of the change or schedule (with offset). <c>Status</c> is one of <see cref="PublishingCalendarEventStatus"/>.
/// <c>VersionStatus</c> is the latest <see cref="CMS.ContentEngine.VersionStatus"/> name.
/// </remarks>
internal record PublishingCalendarEvent(
    string Id,
    string Day,
    string Title,
    DateTimeOffset Date,
    string Status,
    string VersionStatus,
    string ContentType,
    string ContentTypeType,
    string ContentTypeIcon,
    string ChannelName,
    string ChannelType,
    string WorkspaceName,
    string LanguageName,
    string LanguageDisplayName,
    string WorkflowName,
    string WorkflowStepName,
    string WorkflowStepIcon,
    DateTimeOffset? ScheduledPublishWhen,
    DateTimeOffset? ScheduledUnpublishWhen,
    DateTimeOffset ModifiedWhen,
    string ModifiedBy,
    string EditUrl);
