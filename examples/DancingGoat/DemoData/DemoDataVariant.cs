using System;

using CMS.ContentEngine;
using CMS.ContentWorkflowEngine;

namespace DancingGoat.DemoData;

/// <summary>
/// Current state of one content item language variant, as loaded from the database.
/// </summary>
public sealed record DemoDataVariant(
    int MetadataId,
    int ContentItemId,
    Guid ContentItemGuid,
    string LanguageName,
    string ClassName,
    string ContentTypeType,
    VersionStatus LatestVersionStatus,
    string WorkflowStepName,
    ContentWorkflowStepType? WorkflowStepType,
    DateTime? ScheduledPublishWhen,
    DateTime? ScheduledUnpublishWhen,
    int WebPageItemId,
    int WebsiteChannelId,
    string WorkflowName)
{
    /// <summary>Stable identity used for slotting: content item GUID + language.</summary>
    public string Key => $"{ContentItemGuid:N}|{LanguageName}";


    public bool IsWebPage => WebPageItemId > 0;


    /// <summary>Status the publishing calendar shows for the activity entry.</summary>
    public string CalendarStatus
    {
        get
        {
            if (ScheduledPublishWhen.HasValue)
            {
                return "scheduled-publish";
            }

            return LatestVersionStatus switch
            {
                VersionStatus.Published => "published",
                VersionStatus.Unpublished => "unpublished",
                _ when WorkflowStepType == ContentWorkflowStepType.Custom => "in-workflow",
                _ => "draft",
            };
        }
    }
}
