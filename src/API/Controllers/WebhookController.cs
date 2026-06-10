using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhookController : APIController
{
    private readonly ILogger<WebhookController> _logger;
    private readonly MongoConnection _connection;
    private readonly LeadBuilderService _leadBuilderService;
    private readonly IEntityIdentityAdapter _entityAdapter;
    private readonly ILeadTypeAdapter _leadTypeAdapter;
    private readonly IAppointmentTypeAdapter _appointmentTypeAdapter;

    public WebhookController(
        ILogger<WebhookController> logger,
        MongoConnection connection,
        LeadBuilderService leadBuilderService,
        IEntityIdentityAdapter entityAdapter,
        ILeadTypeAdapter leadTypeAdapter,
        IAppointmentTypeAdapter appointmentTypeAdapter
        )
    {
        _logger = logger;
        _connection = connection;
        _leadBuilderService = leadBuilderService;
        _entityAdapter = entityAdapter;
        _leadTypeAdapter = leadTypeAdapter;
        _appointmentTypeAdapter = appointmentTypeAdapter;
    }

    [HttpGet("/post/{id}")]
    public async Task<NewLeadResult> PostUsingGetAsync(Guid id)
    {
        var body = new ExpandoObject();
        return await AddLeadAsync(id, body);
    }

    [HttpPost("/post/{id}")]
    [Consumes("application/json")]
    public async Task<NewLeadResult> PostJsonAsync(Guid id, [FromBody] ExpandoObject body)
    {
        var result = await AddLeadAsync(id, body);
        return result;
    }

    [HttpPost("/post/{id}")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<NewLeadResult> PostFormAsync(Guid id)
    {
        return await ParseFormAsync(id);
    }

    [HttpPost("/post/{id}/unbounce")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<NewLeadResult> PostUnbounceFormAsync(Guid id)
    {
        dynamic obj = new ExpandoObject();

        var dict = obj as IDictionary<string, object>;
        foreach (var key in Request.Form.Keys)
        {
            if (Request.Form.TryGetValue(key, out var value))
            {
                var objValue = (value.Count == 1) ? (object)value[0] : value;
                if (string.Equals(key, "data.json") && objValue is string str)
                {
                    foreach (var prop in JsonConvert.DeserializeObject<Dictionary<string, string[]>>(str))
                    {
                        if (prop.Value == null || prop.Value.Length == 0) continue;
                        dict.TryAdd($"unbounce_{prop.Key}", prop.Value.Length == 1 ? prop.Value[0] : prop.Value);
                    }
                }

                dict.TryAdd(key, objValue);
            }
        }

        // TODO: FILES 
        // ...

        return await AddLeadAsync(id, obj);
    }

    private Task<NewLeadResult> ParseFormAsync(Guid leadTypeId)
    {
        dynamic obj = new ExpandoObject();

        var dict = obj as IDictionary<string, object>;
        foreach (var key in Request.Form.Keys)
        {
            if (Request.Form.TryGetValue(key, out var value))
            {
                if (value.Count == 1)
                {
                    dict.Add(key, value[0]);
                }
                else
                {
                    dict.Add(key, value);
                }
            }
        }

        // TODO: FILES 
        // ...

        return AddLeadAsync(leadTypeId, obj);
    }

    [HttpPost("/post/{id}")]
    [Consumes("application/xml")]
    public async Task<NewLeadResult> PostXmlAsync(Guid id)
    {
        var body = ParseXml2();
        return await AddLeadAsync(id, body);
    }

    private object ParseXml2()
    {
        XDocument doc = XDocument.Parse(Request.GetBody());
        string jsonText = JsonConvert.SerializeXNode(doc);
        dynamic dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
        return dyn;
    }

    private async Task<ActionResult> ParseXmlAsync()
    {
        // https://docs.microsoft.com/en-us/dotnet/standard/data/xml/xml-processing-options

        XmlReaderSettings settings = new XmlReaderSettings();
        settings.Async = true;
        settings.DtdProcessing = DtdProcessing.Ignore;
        settings.IgnoreComments = true;
        settings.ValidationType = ValidationType.None;

        using (XmlReader reader = XmlReader.Create(Request.Body, settings))
        {
            while (await reader.ReadAsync())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        Console.WriteLine("Start Element {0}", reader.Name);
                        break;

                    case XmlNodeType.Text:
                        Console.WriteLine("Text Node: {0}",
                        await reader.GetValueAsync());
                        break;

                    case XmlNodeType.EndElement:
                        Console.WriteLine("End Element {0}", reader.Name);
                        break;

                    default:
                        Console.WriteLine("Other node {0} with value {1}", reader.NodeType, reader.Value);
                        break;
                }
            }
        }

        return Ok(Request.GetBody());
    }

    private LeadRequest BuildRequestObject(Guid leadTypeId)
    {
        var request = LeadBuilderService.BuildLeadRequestObject(HttpContext);
        request.LeadTypeId = leadTypeId;
        return request;
    }

    private async Task<NewLeadResult> AddLeadAsync(Guid leadTypeId, dynamic payload)
    {
        var request = BuildRequestObject(leadTypeId);

        payload.Request = request.Request;
        request.Body = JsonConvert.SerializeObject(payload);

        await _connection.InsertAsync(request);

        try
        {
            var leadType = await _leadTypeAdapter.GetByIdAsync(leadTypeId);
            if (leadType == null) throw new NotFoundException(nameof(LeadType), leadTypeId);

            var entity = await _entityAdapter.GetEntityByIdAsync(leadType.EntityId);
            if (entity == null) throw new NotFoundException(nameof(Entity), leadType.EntityId);

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                {"leadTypeId", leadTypeId},
                {"leadType",  leadType.Name},
                {"entityId", leadType.EntityId},
                {"entity", entity.Name},
                {"leadRequestId", request.Id},
            });

            var builder = await _leadBuilderService.AddAsync(entity.Context, leadType, request.Body);
            if (builder.Failed)
            {
                _logger.LogError("{Result}: {Error}", "failed", builder.Error);
                request.Error = builder.Error;
                throw new BadRequestException(builder.Error);
            }

            _logger.LogInformation("{Result}: {LeadId} added", "success", builder.Result.Id);

            entity = await _entityAdapter.GetEntityByIdAsync(builder.Result.EntityId);
            var appointmentType = entity.Context.Role switch
            {
                EntityRoleId.Organization => await _appointmentTypeAdapter.GetDefaultForOrgAsync(entity.Context, leadType.Id),
                _ => null
            };

            request.Response = new NewLeadResult
            {
                LeadId = builder.Result.Id,
                EntityId = builder.Result.EntityId,
                DefaultAppointmentTypeId = appointmentType?.Id,
                ContentType = Request.ContentType
            };

            return request.Response;
        }
        catch (BadRequestException ex)
        {
            request.Error ??= ex.Message;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{LeadRequestId}: {Error}", request.Id, ex.Message);
            request.Error ??= ex.Message;
            throw;
        }
        finally
        {
            // update request
            await _connection.Filter<LeadRequest>()
                .Eq(x => x.Id, request.Id)
                .Update
                    .Set(x => x.Response, request.Response)
                    .Set(x => x.Error, request.Error)
                    .Set(x => x.FinishedOn, DateTime.UtcNow)
                .UpdateOneAsync();
        }
    }
}
