using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardWeb;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Reports.Controllers;

[Authorize("default")]
[ApiExplorerSettings(IgnoreApi = true)]
public class MainDashboardController : DashboardController
{
    public MainDashboardController(DashboardConfigurator configurator, IDataProtectionProvider dataProtectionProvider = null)
        : base(configurator, dataProtectionProvider)
    {
    }
}