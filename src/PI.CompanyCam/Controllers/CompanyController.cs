using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.CompanyCam.Services;
using PI.Shared.Controllers;

namespace PI.CompanyCam.Controllers;

[Authorize("default")]
[Route("/companycam/v1/[controller]")]
public class CompanyController : APIController
{
    private readonly CompanyCamService _service;

    public CompanyController(CompanyCamService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<Company> GetCompanyInfoAsync()
    {
        var client = await _service.GetClientAsync(Context);
        var company = await client.GetCurrentCompanyAsync();
        return company;
    }
    
    [HttpGet("Owner")]
    public async Task<User> GetCompanyOwnerAsync()
    {
        var client = await _service.GetClientAsync(Context);
        var user = await client.GetCurrentUserAsync();
        return user;
    }
}