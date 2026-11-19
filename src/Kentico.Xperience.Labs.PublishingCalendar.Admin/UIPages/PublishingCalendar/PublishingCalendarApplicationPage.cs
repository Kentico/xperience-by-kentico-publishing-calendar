using CMS.Membership;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.PublishingCalendar.Admin;

[assembly: UIApplication(
    identifier: PublishingCalendarApplicationPage.IDENTIFIER,
    type: typeof(PublishingCalendarApplicationPage),
    slug: "publishing-calendar",
    name: "Publishing calendar (Labs)",
    category: BaseApplicationCategories.CONTENT_MANAGEMENT,
    icon: Icons.Calendar,
    templateName: TemplateNames.SECTION_LAYOUT)]

namespace Kentico.Xperience.Labs.PublishingCalendar.Admin;

[UIPermission(SystemPermissions.VIEW)]
internal class PublishingCalendarApplicationPage : ApplicationPage
{
    public const string IDENTIFIER = "publishing-calendar-app";
}
