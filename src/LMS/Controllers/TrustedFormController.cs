using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PI.Shared.Controllers;
using TrustedForm;

namespace LMS.Controllers;

[Authorize("admin")]
public class TrustedFormController : APIController
{
    private readonly ActiveProspectConfig _config;
    private readonly HttpClient _client;

    public TrustedFormController(IConfiguration configuration, IHttpClientFactory clientFactory)
    {
        _config = configuration.GetSection("ActiveProspect").Get<ActiveProspectConfig>();
        _client = clientFactory.CreateClient("ActiveProspect");
    }
    
    private Task<CertificateOperationResponse> RequestAsync(string id, CertificateOperationRequest body)
    {
        return body.Execute(_client, _config.APIKey, id);

        // var client = new CertificateURLApi
        // {
        //     Configuration = new Configuration
        //     {
        //         Username = "API",
        //         Password = _config.APIKey,
        //     },
        // };
        //
        // return await client.CertificateOperationAsync("application/json", id, "4.0", request);
    }

    [HttpGet("/lms/v1/[controller]/{id}/Validate")]
    public async Task<MatchLeadResult> ValidateAsync([FromRoute] string id, [FromQuery] string email, [FromQuery] string phone)
    {
        var result = await RequestAsync(id, new CertificateOperationRequest
            {
                MatchLead =  new MatchLeadPhoneEmailParameters
                {
                    Email = email,
                    Phone = phone,
                },
            }
        );

        return result.MatchLead;
    }

    [HttpPost("/lms/v1/[controller]/{id}")]
    public async Task<CertificateOperationResponse> BulkAsync([FromRoute] string id, [FromBody] InsightsRequest req)
    {
        var request = new CertificateOperationRequest
        {
            MatchLead = new MatchLeadPhoneEmailParameters
            {
                Email = req.Email,
                Phone = req.Phone,
            },
        };
        
        if (req.Insights?.Length > 0)
        {
            request.Insights = new InsightsParameters
            {
                Properties = req.Insights.ToList(),
            };
        }
        
        if (!string.IsNullOrEmpty(req.Vendor) && !string.IsNullOrEmpty(req.VendorId))
        {
            request.Retain = new RetainParameters
            {
                Reference = req.VendorId,
                Vendor = req.Vendor,
            };
        }
        
        var result = await RequestAsync(id,request);
        
        return result;
    }

    [HttpGet("/lms/v1/[controller]/{id}/Retain")]
    public async Task<RetainResult> RetainAsync([FromRoute] string id, [FromQuery] string email, [FromQuery] string phone, [FromQuery] string vendor, [FromQuery] string vendorId)
    {
        var result = await RequestAsync(id, new CertificateOperationRequest
            {
                MatchLead = new MatchLeadPhoneEmailParameters
                {
                    Email = email,
                    Phone = phone,
                },
                Retain = new RetainParameters
                {
                    Reference = vendorId,
                    Vendor = vendor,
                },
            }
        );

        return result.Retain;
    }
}

public class InsightsRequest
{
    public string Email { get; set; }
    public string Phone { get; set; }

    public InsightsParameters.PropertiesEnum[] Insights { get; set; }
    
    public string Vendor { get; set; } 
    public string VendorId { get; set; }
}