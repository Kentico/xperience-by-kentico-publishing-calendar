using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.PublishingCalendar.Admin;

[assembly: CMS.RegisterModule(typeof(PublishingCalendarAdminModule))]

namespace Kentico.Xperience.Labs.PublishingCalendar.Admin;

internal class PublishingCalendarAdminModule : AdminModule
{
    public PublishingCalendarAdminModule()
        : base("Kentico.Xperience.Labs.PublishingCalendar.Admin")
    {
    }

    protected override void OnInit()
    {
        base.OnInit();

        // Makes the module accessible to the admin UI
        // Must match AdminOrgName/ProjectName in the .csproj and orgName/projectName in Client/webpack.config.js
        RegisterClientModule("kentico", "xperience-labs-publishing-calendar-admin");
    }
}
