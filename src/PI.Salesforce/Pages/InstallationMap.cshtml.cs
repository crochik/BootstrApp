using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PI.Salesforce.Models;

namespace PI.Salesforce.Pages;

// [Authorize("default")]
public class InstallationMap : PageModel
{
    private readonly InstallationMapLoader _loader;

    public PI.Salesforce.Models.InstallationMap Model { get; set; }

    public InstallationMap(InstallationMapLoader loader)
    {
        _loader = loader;
    }

    public async Task OnGetAsync([FromRoute] Guid redirectionId)
    {
        Model = await _loader.LoadAsync(redirectionId);
    }
}