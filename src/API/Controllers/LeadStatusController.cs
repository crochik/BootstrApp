using System;
using AutoMapper;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Adapters;

namespace Controllers;

[Obsolete]
[Authorize("default")]
[Route("/api/v1/[controller]")]
public class LeadStatusController : AbstractNewModelController<ILeadStatusAdapter, PI.Shared.Models.ILeadStatus, LeadStatus>
{
    public LeadStatusController(ILogger<LeadStatusController> logger, IMapper mapper, ILeadStatusAdapter adapter) :
        base(logger, mapper, adapter)
    {
    }
}