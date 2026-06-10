using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using Controllers;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.Shared.Controllers;

namespace LMS.Controllers;

/// <summary>
/// first implementation using Handlers...
/// for most (if not all) it will run v2 code because of Version2InterceptorHandler
/// TODO: probably can be fully replaced by the code in V2
/// ....
/// </summary>
[Obsolete("replace with LeadV2Controller")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Produces("application/json")]
[Route("/lms/v1/[controller]")]
public class LeadController : APIController
{
    private static readonly HashAlgorithm HashAlgo = SHA256.Create();

    private readonly NewLeadService _service;

    public LeadController(NewLeadService service)
    {
        _service = service;
    }

    // [HttpGet("{id}")]
    // public async Task<NewLeadResult> PostUsingGetAsync(Guid id)
    // {
    //     var body = new ExpandoObject();
    //     return await AddLeadAsync(id, body);
    // }

    [HttpPost("{id}")]
    [Consumes("application/json")]
    public async Task<ActionResult> PostJsonAsync(Guid id, [FromBody] ExpandoObject body)
    {
        var result = await AddLeadAsync(id, body);
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
        return await AddLeadAsync(id, body);
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

        return AddLeadAsync(leadTypeId, obj);
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

    private async Task<ActionResult> AddLeadAsync(Guid leadTypeId, ExpandoObject payload)
    {
        var request = BuildLeadRequestObject(HttpContext, leadTypeId, payload);
        return await _service.HandleAsync(request);
    }
}