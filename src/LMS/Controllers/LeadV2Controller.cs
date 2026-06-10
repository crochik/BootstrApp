using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Controllers;
using Crochik.Mongo;
using LMS.Models;
using Messages.Flow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Data.Models;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services.ActionRunners;

namespace LMS.Controllers;

/// <summary>
/// New implementation not using handlers
/// not in use but should replace the LeadController 
/// </summary>
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Produces("application/json")]
[Route("/lms/v2/Lead")]
public class LeadV2Controller : APIController
{
    private static readonly HashAlgorithm HashAlgo = SHA256.Create();
    private readonly ILogger<LeadV2Controller> _logger;
    private readonly ActionRunnerService _service;
    private readonly MongoConnection _connection;

    public LeadV2Controller(ILogger<LeadV2Controller> logger, ActionRunnerService service, MongoConnection connection)
    {
        _logger = logger;
        _service = service;
        _connection = connection;
    }

    [HttpPost("{id}")]
    [Consumes("application/json")]
    public async Task<ActionResult> PostJsonAsync(Guid id, [FromBody] ExpandoObject body)
    {
        var result = await AddLeadV2Async(id, body);
        return result;
    }

    [HttpPost("{id}")]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<ActionResult> PostFormAsync(Guid id)
    {
        return await ParseFormAsync(id);
    }

    [HttpPost("{id}")]
    [Consumes("application/xml")]
    public async Task<ActionResult> PostXmlAsync(Guid id)
    {
        var body = ParseXml2();
        return await AddLeadV2Async(id, body);
    }

    private Task<ActionResult> ParseFormAsync(Guid leadTypeId)
    {
        var encoded = Request.ContentType?.Contains("application/x-www-form-urlencoded") ?? false;
        dynamic obj = new ExpandoObject();

        var dict = obj as IDictionary<string, object>;
        foreach (var key in Request.Form.Keys)
        {
            if (!Request.Form.TryGetValue(key, out var value)) continue;

            if (value.Count == 1)
            {
                var paramValue = encoded ? HttpUtility.UrlDecode(value[0], Encoding.Default) : value[0];
                dict.Add(key, paramValue);
            }
            else
            {
                dict.Add(key, value);
            }
        }

        // TODO: FILES 
        // ...

        return AddLeadV2Async(leadTypeId, obj);
    }

    private ExpandoObject ParseXml2()
    {
        XDocument doc = XDocument.Parse(Request.GetBody());
        string jsonText = JsonConvert.SerializeXNode(doc);
        var dyn = JsonConvert.DeserializeObject<ExpandoObject>(jsonText);
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

    private string HashAuthorization(string header)
    {
        // TODO: add some salt
        // ...
        byte[] bytes = HashAlgo.ComputeHash(Encoding.UTF8.GetBytes(header));
        return Convert.ToBase64String(bytes);
    }

    private Request BuildLeadRequestObject(HttpContext httpContext, Guid leadTypeId, ExpandoObject payload)
    {
        var httpRequest = httpContext.Request;
        var leadRequest = new Request
        {
            Path = httpRequest.Path.ToString(),
            Host = httpRequest.Host.ToString(),
            ContentType = httpRequest.ContentType,
            Method = httpRequest.Method,
            ContentLength = httpRequest.ContentLength,
            TraceIdentifier = httpContext.TraceIdentifier,
            LeadTypeId = leadTypeId,
            Payload = payload,
        };

        foreach (var parm in httpRequest.Query)
        {
            leadRequest.Query ??= new Dictionary<string, object>();
            var value = parm.Value.Count == 1 ? (object)parm.Value.ToString() : parm.Value.ToArray();
            leadRequest.Query.Add(parm.Key, value);
        }

        foreach (var header in httpRequest.Headers)
        {
            switch (header.Key)
            {
                case "Referer":
                    leadRequest.Headers.Referer = header.Value.ToString();
                    break;
                case "Origin":
                    leadRequest.Headers.Origin = header.Value.ToString();
                    break;
                case "User-Agent":
                    leadRequest.Headers.UserAgent = header.Value.ToString();
                    break;
                case "Authorization":
                    leadRequest.Headers.AuthorizationHash = HashAuthorization(header.Value.ToString());
                    break;
                case "X-Forwarded-For":
                    leadRequest.RemoteIp = header.Value.ToString();
                    break;
            }
        }

        if (string.IsNullOrEmpty(leadRequest.RemoteIp))
        {
            // fallback if didn't find forward header
            leadRequest.RemoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        }

        return leadRequest;
    }

    private async Task<ActionResult> AddLeadV2Async(Guid leadTypeId, ExpandoObject payload)
    {
        var request = BuildLeadRequestObject(HttpContext, leadTypeId, payload);

        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.Id, leadTypeId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (leadType == null) throw new ForbiddenException();

        if (string.IsNullOrEmpty(leadType.ObjectType) || !leadType.TransactionFlowId.HasValue || !leadType.TransactionObjectStatusId.HasValue)
        {
            _logger.LogError("{LeadTypeId} not configured for v2", leadTypeId);
            return Forbid();
        }

        var accountContext = new AccountContext(leadType.AccountId);

        var transaction = new Transaction
        {
            Id = request.Id,
            Request = request,
            AccountId = leadType.AccountId,
            FlowId = leadType.TransactionFlowId,
            ObjectStatusId = leadType.TransactionObjectStatusId,
            Tags = new[] { "LMS" },
        };

        await _connection.InsertAsync(transaction);

        var initialEvent = new GenericFlowEvent
        {
            ObjectType = Transaction.ObjectTypeName,
            TargetId = transaction.Id,
            EventTypeId = EventIds.OnStatusEntered,
            FlowId = transaction.FlowId.Value,
            StatusId = transaction.ObjectStatusId.Value,
        };

        try
        {
            await _service.RunAsync(accountContext, initialEvent);

            transaction = await _connection.Filter<Transaction>()
                .Eq(x => x.Id, transaction.Id)
                .FirstOrDefaultAsync();

            if (transaction.Response != null)
            {
                // TODO: handle different response formats
                // ...
                return new OkObjectResult(new
                {
                    transaction.Response.Success,
                    transaction.Response.Reason,
                    LeadId = transaction.Response.Lead?.Id,
                    RequestId = transaction.Request.Id,
                })
                {
                    StatusCode = (int)(transaction.Response.Success ? HttpStatusCode.Created : HttpStatusCode.BadRequest),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {TransactionId}", transaction.Id);
        }
        
        return new OkObjectResult(new
        {
            Success = false,
            Reason = "INTERNAL_ERROR",
            LeadId = default(Guid?),
            RequestId = transaction.Request.Id,
        })
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
        };
    }
}