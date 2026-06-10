using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Web.WebDocumentViewer;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace Reports.Services
{
    public class WebDocumentViewerReportResolver : IWebDocumentViewerReportResolver
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly BridgeService _bridge;

        private IEntityContext Context => _contextAccessor.HttpContext.GetContextWithActor();

        public WebDocumentViewerReportResolver(
            IHttpContextAccessor contextAccessor,
            BridgeService bridge
            )
        {
            _contextAccessor = contextAccessor;
            _bridge = bridge;
        }

        public XtraReport Resolve(string reportEntry)
        {
            if ( reportEntry=="blank")
            {
                return new XtraReport
                {
                    Name = "blank",
                    DisplayName = "Blank"
                };
            }

            var report = _bridge.LoadReportAsync(Context, reportEntry).Result;
            return report;
        }
    }
}