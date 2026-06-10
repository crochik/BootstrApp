using System.Threading.Tasks;
using DevExpress.XtraReports.Web.ReportDesigner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using Reports.Services;

namespace Reports.Controllers;

[Route("/reports/v1/Config")]
[Authorize("admin")]
public class ConfigController : APIController
{
    private readonly BridgeService _bridge;

    public ConfigController(BridgeService bridge)
    {
        this._bridge = bridge;
    }

    [HttpPost("Model")]
    public async Task<ActionResult> Model([FromForm] string reportUrl, [FromServices] IReportDesignerClientSideModelGenerator reportDesignerClientSideModelGenerator)
    {
        var sources = await _bridge.GetAvailableDataSourcesAsync(Context);

        string modelJsonScript =
            reportDesignerClientSideModelGenerator
                .GetJsonModelScript(
                    // The name of a report (reportUrl)
                    // that the Report Designer opens when the application starts.
                    reportUrl,
                    // Data sources for the Report Designer.                
                    sources,
                    // The URI path of the default controller
                    // that processes requests from the Report Designer.
                    "/reports/v1/Designer",
                    // The URI path of the default controller
                    // that processes requests from the Web Document Viewer.
                    "/reports/v1/Viewer",
                    // The URI path of the default controller
                    // that processes requests from the Query Builder.
                    "/reports/v1/QueryBuilder"
                );

        return Content(modelJsonScript, "application/json");
    }
}