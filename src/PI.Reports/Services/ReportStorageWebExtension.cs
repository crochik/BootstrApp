using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace Reports.Services
{
    public class ReportStorageWebExtension : DevExpress.XtraReports.Web.Extensions.ReportStorageWebExtension
    {
        private readonly ILogger<ReportStorageWebExtension> _logger;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly BridgeService _bridge;

        private IEntityContext Context => _contextAccessor.HttpContext.GetContextWithActor();

        public ReportStorageWebExtension(
            ILogger<ReportStorageWebExtension> logger,
            IHttpContextAccessor contextAccessor,
            BridgeService bridge
            )
        {
            _logger = logger;
            _contextAccessor = contextAccessor;
            _bridge = bridge;
        }

        /// <summary>
        /// Is writable?
        /// </summary>
        public override bool CanSetData(string url)
        {
            return true;
        }

        /// <summary>
        /// is valid url
        /// </summary>
        public override bool IsValidUrl(string url)
        {
            return true;
        }

        public override async Task<Dictionary<string, string>> GetUrlsAsync()
        {
            return await _bridge.GetListAsync(Context);
        }

        public override async Task<byte[]> GetDataAsync(string url)
        {
            return await _bridge.LoadReportDataAsync(Context, url);
        }

        public override async Task SetDataAsync(XtraReport report, string url)
        {
            await _bridge.SaveAsync(Context, report, url);
        }

        public override async Task<string> SetNewDataAsync(XtraReport report, string defaultUrl)
        {
            return await _bridge.CreateAsync(Context, report, defaultUrl);
        }
    }
}