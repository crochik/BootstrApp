using System;
using DevExpress.XtraReports.Web.ClientControls;
using Microsoft.Extensions.Logging;

namespace Reports.Services
{
    public class ReportingLoggerService : LoggerService
    {
        private readonly ILogger<ReportingLoggerService> _logger;
        
        public ReportingLoggerService(ILogger<ReportingLoggerService> logger)
        {
            _logger = logger;
        }

        public override void Error(Exception exception, string message)
        {
            _logger.LogError(exception, message);
        }

        public override void Info(string message)
        {
            _logger.LogInformation(message);
        }
    }
}