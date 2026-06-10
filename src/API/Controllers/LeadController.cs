using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Data;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.OpenAPI;
using PI.Shared.Services;
using Lead = PI.Shared.Models.Lead;

namespace Controllers;

[Produces("application/json")]
[Route("/api/v1/[controller]")]
public class LeadController : APIController
{
    private static readonly IQueryParams _defaultQuery = new QueryParams(100, $"{nameof(Lead.CreatedOn)} DESC");

    private static readonly Dictionary<FIELDTYPE, Type> FieldMap = new()
    {
        { FIELDTYPE.Undefined, typeof(GenericField) },
        { FIELDTYPE.Address, typeof(AddressField) },
        { FIELDTYPE.Boolean, typeof(CheckboxField) },
        { FIELDTYPE.Date, typeof(DateField) },
        { FIELDTYPE.Datetime, typeof(DateTimeField) },
        { FIELDTYPE.Email, typeof(EmailField) },
        { FIELDTYPE.Hidden, typeof(HiddenField) },
        { FIELDTYPE.Number, typeof(NumberField) },
        { FIELDTYPE.Phone, typeof(PhoneField) },
        { FIELDTYPE.Postalcode, typeof(PostalCodeField) },
        { FIELDTYPE.Text, typeof(TextField) },
        { FIELDTYPE.Time, typeof(TimeField) },
    };

    private static FormField Build(FieldMapperConfig config)
    {
        var field = Activator.CreateInstance(FieldMap[config.Type]) as FormField;
        field.DefaultValue = config.DefaultValue;
        field.IsRequired = config.IsRequired;
        field.Name = config.Name;
        field.Label = config.Label;
        field.DefaultValue = config.DefaultValue;

        return field;
    }

    private readonly IMapper _mapper;
    private readonly MongoConnection _connection;
    private readonly ILeadAdapter _leadAdapter;
    private readonly ILeadTypeAdapter _leadTypeAdapter;
    private readonly LeadBuilderService _leadBuilderService;

    public LeadController(
        IMapper mapper,
        MongoConnection connection,
        ILeadAdapter leadAdapter,
        ILeadTypeAdapter leadTypeAdapter,
        LeadBuilderService leadBuilderService
    )
    {
        _mapper = mapper;
        _connection = connection;
        _leadAdapter = leadAdapter;
        _leadTypeAdapter = leadTypeAdapter;
        _leadBuilderService = leadBuilderService;
    }

    private async Task<Leads> MapAsync(Guid leadTypeId, IEnumerable<Lead> leads)
    {
        var leadType = await _leadTypeAdapter.GetByIdAsync(leadTypeId);
        var config = _leadBuilderService.GetMapper(leadType);

        var fields = config.Fields.Select(x => Build(x));

        return new Leads
        {
            LeadTypeId = leadTypeId,
            Fields = fields, //_mapper.Map<List<PI.Shared.Form.Models.FormField>>(config.Fields),
            List = leads.Select(lead => _mapper.Map<Models.Lead>(lead)).ToList()
        };
    }

    [Authorize("admin")]
    [HttpPost("/api/v1/LeadType({leadTypeId})/[controller]/Import")]
    public async Task<int> ImportLeadsAsync([FromRoute] Guid leadTypeId, IFormFile file, [FromServices] LeadBuilderService service)
    {
        var leadType = await _connection.Filter<PI.Shared.Data.Models.LeadType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, leadTypeId)
            .FirstOrDefaultAsync();

        if (leadType == null) throw NotFoundException.New<LeadType>(leadTypeId);

        return await service.ImportCSVAsync(Context, leadType, file.OpenReadStream(), false);
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})")]
    public async Task<Leads> GetAsync(Guid id)
    {
        var lead = await _leadAdapter.GetByIdAsync(Context, id);
        if (lead == null) throw new NotFoundException(SystemObjectType.Lead, id);

        return await MapAsync(lead.LeadTypeId, new[] { lead });
    }

    [Authorize("default")]
    [HttpGet("/api/v1/LeadType({id})/[controller]")]
    public async Task<Leads> GetByTypeAsync([FromRoute] Guid id)
    {
        var leads = await _leadAdapter.GetByTypeAsync(Context, id, _defaultQuery);
        if (leads == null) throw new NotFoundException(SystemObjectType.Lead, id);
        return await MapAsync(id, leads);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize("admin")]
    [HttpPut("/api/v1/LeadType({id})/[controller]")]
    [StringBody]
    public async Task<Leads> UpsertLeadAsync([FromRoute] Guid id)
    {
        var leadType = await _leadTypeAdapter.GetByIdAsync(id);
        var body = Request.GetBody()?.Trim();
        var builder = await _leadBuilderService.AddAsync(Context, leadType, body);
        if (builder.Failed) throw new BadRequestException(builder.Error);

        var lead = builder.Result;
        return await MapAsync(lead.LeadTypeId, new[] { lead });
    }
    
    [Authorize("default")]
    [HttpPost("search")]
    [ProducesResponseType(typeof(LeadSearchResults), 200)]
    public async Task<LeadSearchResults> SearchAsync([FromBody] Search search)
    {
        if (search == null || search.Top > 500) throw new BadRequestException("Invalid request");
        if (search.Top == 0) search.Top = 100;

        var result = await _leadAdapter.SearchAsync(Context, search);

        return result;
    }
    
    [Authorize("default")]
    [HttpGet("/api/v1/[controller]({id})/Preview/DataForm")]
    public async Task<Form> GetEditFormAsync([FromRoute] Guid id)
    {
        var lead = await _leadAdapter.GetByIdAsync(Context, id);
        if (lead == null) throw new NotFoundException(SystemObjectType.Lead, id);

        var leadType = await _leadTypeAdapter.GetByIdAsync(lead.LeadTypeId);
        var config = _leadBuilderService.GetMapper(leadType);

        var prosp = new Dictionary<string, object>(lead.AllProperties());
        var fields = config.Fields.Select(x =>
        {
            var field = Build(x);
            if (prosp.TryGetValue(field.Name, out var value))
            {
                field.DefaultValue = value;
            }

            return field;
        });

        // var result = await _objectTypeService.GetDataFormAsync(Context, objectType, id);B
        // if (result == null) throw new NotFoundException();

        return new Form
        {
            Title = lead.Name,
            Name = nameof(Lead),
            Fields = fields.ToArray(),
            // Actions 
        };
    }

    /// <summary>
    /// Test end point to get leads in a map
    /// </summary>
    [Authorize("default")]
    [HttpPost("Map/DataView")]
    [Produces("text/csv", "application/json")]
    public async Task<IDataViewResponse> GetMapViewAsync([FromBody] DataViewRequest request, [FromServices] ObjectTypeService objectTypeService, [FromServices] ObjectDataViewBuilder builder)
    {
        var objectType = await objectTypeService.GetAsync(Context, nameof(Lead));
        var response = await builder.BuildDataViewAsync(Context, objectType, request);

        response.View.Title = "Near Me";
        
        // if (response.Request.Fields?.Contains(nameof(Lead.Location)) ?? false)
        // {
        //     response.Options = new MapViewOptions();    
        // }

        return response;
    }
}