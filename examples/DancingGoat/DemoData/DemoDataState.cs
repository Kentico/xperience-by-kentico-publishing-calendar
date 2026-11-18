namespace DancingGoat.DemoData;

/// <summary>
/// State the demo data refresh gives a language variant.
/// </summary>
public enum DemoDataState
{
    /// <summary>Published, no draft.</summary>
    Published,

    /// <summary>Draft (workflow draft step for workflow types) over the published version.</summary>
    Draft,

    /// <summary>Draft in a custom workflow step over the published version.</summary>
    InWorkflow,

    /// <summary>Unpublished; not on the live site.</summary>
    Unpublished,

    /// <summary>Draft over the published version, scheduled to be published.</summary>
    ScheduledPublish,

    /// <summary>Published, scheduled to be unpublished.</summary>
    ScheduledUnpublish,

    /// <summary>Never-published drafts and emails: status kept, dates refreshed.</summary>
    Unchanged,
}
