using System.Threading.Tasks;
using DevExpress.XtraReports.Services;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace Reports.Services
{
    public class ReportProviderAsync : IReportProviderAsync
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly BridgeService _bridge;
        private IEntityContext Context => _contextAccessor.HttpContext.GetContextWithActor();

        public ReportProviderAsync(
            IHttpContextAccessor contextAccessor,
            BridgeService bridge
            )
        {
            _contextAccessor = contextAccessor;
            _bridge = bridge;
        }

        public async Task<XtraReport> GetReportAsync(string id, ReportProviderContext context)
        {
            return await _bridge.LoadReportAsync(Context, id);
        }
    }
}