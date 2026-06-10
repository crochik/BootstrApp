using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class ObjectStatusController : APIController
{
    private readonly IMapper _mapper;
    private readonly ObjectTypeService _service;

    public ObjectStatusController(
        IMapper mapper,
        ObjectTypeService service
    )
    {
        _mapper = mapper;
        _service = service;
    }

    // [Obsolete("use other route to avoid confusion")]
    // [Authorize("managerplus")]
    // [HttpGet("/api/v1/{objectType}/[controller]")]
    // public Task<IEnumerable<ObjectStatus>> GetAsync([FromRoute] string objectType) => GetStatusesForObjectTypeAsync(objectType);

    [Authorize("managerplus")]
    [HttpGet("/api/v1/[controller]")]
    public async Task<IEnumerable<ObjectStatus>> GetStatusesForObjectTypeAsync([FromQuery] string objectTypeName)
    {
        var result = await _service.GetStatusesAsync(Context, objectTypeName);
        return result.Select(x => _mapper.Map<ObjectStatus>(x));
    }
}

public class ObjectStatus : ApiEntityOwnedModel
{
    public string ObjectType { get; set; }
}

public class ObjectStatusProfile : Profile
{
    public ObjectStatusProfile()
    {
        CreateMap<PI.Shared.Models.ObjectStatus, ObjectStatus>(MemberList.Destination)
            .ReverseMap();
    }
}