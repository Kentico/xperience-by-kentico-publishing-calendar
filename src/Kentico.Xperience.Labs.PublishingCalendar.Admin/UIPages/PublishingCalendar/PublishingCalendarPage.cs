using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.PublishingCalendar.Admin;

[assembly: UIPage(
    uiPageType: typeof(PublishingCalendarPage),
    parentType: typeof(PublishingCalendarApplicationPage),
    slug: "calendar",
    name: "Calendar",
    templateName: "@kentico/xperience-labs-publishing-calendar-admin/PublishingCalendar",
    order: 0,
    Icon = Icons.Calendar)]

namespace Kentico.Xperience.Labs.PublishingCalendar.Admin;

[UIEvaluatePermission(SystemPermissions.VIEW)]
internal class PublishingCalendarPage(IPageLinkGenerator pageLinkGenerator) : Page<PublishingCalendarPageClientProperties>
{
    private readonly PublishingCalendarDataSource dataSource = new(pageLinkGenerator);

    public const string LOAD_CALENDAR_COMMAND = "LoadCalendar";
    public const string LOAD_DAY_ITEMS_COMMAND = "LoadDayItems";

    /// <summary>
    /// Items load on the client because day boundaries depend on the browser's time zone.
    /// </summary>
    public override async Task<PublishingCalendarPageClientProperties> ConfigureTemplateProperties(PublishingCalendarPageClientProperties properties)
    {
        properties.LoadCalendarCommandName = LOAD_CALENDAR_COMMAND;
        properties.LoadDayItemsCommandName = LOAD_DAY_ITEMS_COMMAND;
        properties.DayItemsPageSize = PublishingCalendarDataSource.DAY_ITEMS_PAGE_SIZE;
        properties.ContentTypes = await PublishingCalendarDataSource.LoadContentTypeOptions(CancellationToken.None);
        properties.Channels = await PublishingCalendarDataSource.LoadChannelOptions(CancellationToken.None);
        properties.Languages = await PublishingCalendarDataSource.LoadLanguageOptions(CancellationToken.None);
        properties.Workflows = await PublishingCalendarDataSource.LoadWorkflowOptions(CancellationToken.None);

        return properties;
    }

    [PageCommand(CommandName = LOAD_CALENDAR_COMMAND, Permission = SystemPermissions.VIEW)]
    public async Task<ICommandResponse> LoadCalendar(LoadCalendarParams commandParams, CancellationToken cancellationToken) =>
        ResponseFrom(await dataSource.LoadCalendar(commandParams, cancellationToken));

    [PageCommand(CommandName = LOAD_DAY_ITEMS_COMMAND, Permission = SystemPermissions.VIEW)]
    public async Task<ICommandResponse> LoadDayItems(LoadDayItemsParams commandParams, CancellationToken cancellationToken) =>
        ResponseFrom(await dataSource.LoadDayItems(commandParams, cancellationToken));
}
