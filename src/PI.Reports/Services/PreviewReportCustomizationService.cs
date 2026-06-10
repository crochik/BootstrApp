using System.Threading.Tasks;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace Reports.Services
{
    public class PreviewReportCustomizationService : DevExpress.XtraReports.Web.ReportDesigner.Services.PreviewReportCustomizationService
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly BridgeService _bridge;

        public PreviewReportCustomizationService(
            IHttpContextAccessor contextAccessor,
            BridgeService bridge
            )
        {
            _contextAccessor = contextAccessor;
            _bridge = bridge;
        }

        /// <summary>
        /// Called by the designer when previewing the report
        /// intercept to set the actual data 
        /// </summary>
        public override async Task CustomizeReportAsync(XtraReport report)
        {
            var context =  _contextAccessor.HttpContext.GetContextWithActor();
            await _bridge.LoadPreviewAsync(context, report);
            await base.CustomizeReportAsync(report);
        }
    }
}