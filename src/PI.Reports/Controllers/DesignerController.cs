using DevExpress.AspNetCore.Reporting.ReportDesigner;
using DevExpress.AspNetCore.Reporting.ReportDesigner.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Reports.Controllers;

[Authorize("default")]
[Route("/reports/v1/Designer")]
[ApiExplorerSettings(IgnoreApi = true)]
public class DesignerController : ReportDesignerController
{
    public DesignerController(IReportDesignerMvcControllerService controllerService) : base(controllerService)
    {
    }
}

// [Authorize("admin")]
// public class CustomQueryBuilderController : QueryBuilderController
// {
//     public CustomQueryBuilderController(IQueryBuilderMvcControllerService controllerService) : base(controllerService)
//     {
//     }
// }