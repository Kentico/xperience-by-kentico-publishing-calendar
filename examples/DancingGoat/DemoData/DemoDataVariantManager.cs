using System;
using System.Threading;
using System.Threading.Tasks;

using CMS.ContentEngine;
using CMS.ContentWorkflowEngine.Internal;
using CMS.Websites;

namespace DancingGoat.DemoData;

/// <summary>
/// Content operations on one language variant, through the page managers for pages and the content item managers otherwise.
/// </summary>
internal sealed class DemoDataVariantManager
{
    private readonly DemoDataVariant variant;
    private readonly IContentItemManager contentItemManager;
    private readonly IWebPageManager webPageManager;
    private readonly IWorkflowManager workflowManager;


    public DemoDataVariantManager(
        DemoDataVariant variant,
        int userId,
        IContentItemManagerFactory contentItemManagerFactory,
        IWebPageManagerFactory webPageManagerFactory,
        IContentItemWorkflowManagerFactory contentItemWorkflowManagerFactory,
        IWebPageWorkflowManagerFactory webPageWorkflowManagerFactory)
    {
        this.variant = variant;
        if (variant.IsWebPage)
        {
            webPageManager = webPageManagerFactory.Create(variant.WebsiteChannelId, userId);
            workflowManager = webPageWorkflowManagerFactory.Create(userId);
        }
        else
        {
            contentItemManager = contentItemManagerFactory.Create(userId);
            workflowManager = contentItemWorkflowManagerFactory.Create(userId);
        }
    }


    private int ItemId => variant.IsWebPage ? variant.WebPageItemId : variant.ContentItemId;


    private string Language => variant.LanguageName;


    public Task<ContentItemLanguageMetadata> GetMetadata(CancellationToken cancellationToken) =>
        variant.IsWebPage
            ? webPageManager.GetContentItemLanguageMetadata(ItemId, Language, cancellationToken)
            : contentItemManager.GetContentItemLanguageMetadata(ItemId, Language, cancellationToken);


    public Task CancelScheduledPublish(CancellationToken cancellationToken) =>
        variant.IsWebPage
            ? webPageManager.CancelScheduledPublish(ItemId, Language, cancellationToken)
            : contentItemManager.CancelScheduledPublish(ItemId, Language, cancellationToken);


    public Task CancelScheduledUnpublish(CancellationToken cancellationToken) =>
        variant.IsWebPage
            ? webPageManager.CancelScheduledUnpublish(ItemId, Language, cancellationToken)
            : contentItemManager.CancelScheduledUnpublish(ItemId, Language, cancellationToken);


    public async Task CreateDraft(CancellationToken cancellationToken) =>
        Ensure(variant.IsWebPage
            ? await webPageManager.TryCreateDraft(ItemId, Language, cancellationToken)
            : await contentItemManager.TryCreateDraft(ItemId, Language, cancellationToken), "create draft");


    public async Task Publish(CancellationToken cancellationToken) =>
        Ensure(variant.IsWebPage
            ? await webPageManager.TryPublish(ItemId, Language, cancellationToken)
            : await contentItemManager.TryPublish(ItemId, Language, cancellationToken), "publish");


    public async Task Unpublish(CancellationToken cancellationToken) =>
        Ensure(variant.IsWebPage
            ? await webPageManager.TryUnpublish(ItemId, Language, cancellationToken)
            : await contentItemManager.TryUnpublish(ItemId, Language, cancellationToken), "unpublish");


    public Task SchedulePublish(DateTime when, CancellationToken cancellationToken) =>
        variant.IsWebPage
            ? webPageManager.SchedulePublish(ItemId, Language, when, cancellationToken)
            : contentItemManager.SchedulePublish(ItemId, Language, when, cancellationToken);


    public Task ScheduleUnpublish(DateTime when, CancellationToken cancellationToken) =>
        variant.IsWebPage
            ? webPageManager.ScheduleUnpublish(ItemId, Language, when, cancellationToken)
            : contentItemManager.ScheduleUnpublish(ItemId, Language, when, cancellationToken);


    public Task MoveToStep(string stepName, CancellationToken cancellationToken) =>
        workflowManager.MoveToStep(new MoveToStepParameters
        {
            ItemId = ItemId,
            LanguageName = Language,
            StepName = stepName,
        }, cancellationToken);


    private void Ensure(bool succeeded, string operation)
    {
        if (!succeeded)
        {
            throw new InvalidOperationException($"Could not {operation} '{variant.ClassName}' item {variant.ContentItemId} ({Language}).");
        }
    }
}
