using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Reports.Controllers;

[Authorize("default")]
[Route("/reports/v1/Viewer")]
[ApiExplorerSettings(IgnoreApi = true)]
public class ViewerController : WebDocumentViewerController
{
    public ViewerController(IWebDocumentViewerMvcControllerService controllerService) : base(controllerService)
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