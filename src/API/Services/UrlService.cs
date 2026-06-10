using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Services;

namespace Services
{
    public class UrlService : IUrlService
    {
        private readonly string _baseUrl;
        private readonly IDataProtector _protector;
        private readonly ILogger<UrlService> _logger;

        public UrlService(
            ILogger<UrlService> logger,
            IConfiguration configuration,
            IDataProtectionProvider provider
        )
        {
            _baseUrl = configuration.GetValue<string>("BaseUrl");
            _protector = provider.CreateProtector("Services.LeadService"); // typeof(LeadService).FullName
            this._logger = logger;
        }

        public Task<string> GetSchedulerUrlAsync(Guid leadId)
        {
            var bytes = _protector.Protect(leadId.ToByteArray());
            var id = Base64UrlTextEncoder.Encode(bytes);
            return Task.FromResult($"{_baseUrl}/app/Scheduler/{id}");
        }

        public Guid? UnprotectLeadId(string str)
        {
            var bytes = Base64UrlTextEncoder.Decode(str);
            bytes = _protector.Unprotect(bytes);
            if (bytes.Length != 16)
            {
                _logger.LogError($"Invalid Lead Id Length: {bytes.Length}");
                return null;
            }

            return new Guid(bytes);
        }
    }
}