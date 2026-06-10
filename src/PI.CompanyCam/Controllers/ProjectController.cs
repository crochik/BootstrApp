using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.CompanyCam.Services;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace PI.CompanyCam.Controllers;

[Route("/companycam/v1/[controller]")]
public class ProjectController : APIController
{
    private readonly CompanyCamService _service;
    private readonly IHttpClientFactory _httpClientFactory;
    private  HttpClient Client => _httpClientFactory.CreateClient(nameof(ProjectController));

    public ProjectController(CompanyCamService service, IHttpClientFactory httpClientFactory)
    {
        _service = service;
        _httpClientFactory = httpClientFactory;
    }

    [Authorize("admin")]
    [HttpGet]
    public async Task<IEnumerable<Project>> GetProjectsAsync([FromQuery] string search)
    {
        var client = await _service.GetClientAsync(Context);
        client.ReadResponseAsString = true;
        var projects = await client.ListProjectsAsync(null, null, search, null);
        return projects;
    }

    [Authorize("manager")]
    [HttpGet("{objectTypeName}({objectId})/Associate/DataForm")]
    public Form GetAssociateForm([FromRoute] string objectTypeName, string objectId)
    {
        return new Form
        {
            Title = "Attach CompanyCam Project",
            Fields = new FormField[]
            {
                new ReferenceField
                {
                    Name = "CCProjectId",
                    Label = "CompanyCam Project",
                    IsRequired = true,
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = "/companycam/v1/project",
                    }
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Associate",
                    Enable = new[] { "CCProjectId" }
                }
            }
        };
    }

    [Authorize("manager")]
    [HttpPost("{objectTypeName}({objectId})/Associate/DataForm")]
    public async Task<DataFormActionResponse> SaveDataViewAsync([FromRoute] string objectTypeName, string objectId, [FromBody] DataFormActionRequest request)
    {
        if (!Model.TryParseGuid(objectId, out var id)) return DataFormActionResponse.Error(request, "Invalid or Missing id");
        if (!request.TryGetStrParam("CCProjectId", out var projectId)) return DataFormActionResponse.Error(request, "Invalid or Missing CompanyCam Project");

        var result = await _service.AssociateAsync(Context, objectTypeName, id, projectId);
        if (!result.IsSuccess) return DataFormActionResponse.Error(request, result.Status);
        
        return DataFormActionResponse.Error(request, "Success?!");
    }

    [Authorize("default")]
    [HttpPost("Lookup")]
    public async Task<IEnumerable<ReferenceValue>> LookupAsync([FromBody] DataViewRequest request)
    {
        var search = request.Criteria?.FirstOrDefault(x => x.FieldName == Condition.AutoComplete)?.Value?.ToString();

        var client = await _service.GetClientAsync(Context);
        client.ReadResponseAsString = true;
        var projects = await client.ListProjectsAsync(null, null, search, null);
        return projects.Select(x => new ReferenceValue
        {
            Id = x.Id,
            Value = x.Name,
        });
    }

    [Authorize("admin")]
    [HttpPost("/companycam/v1/[controller]({id})/Photo")]
    public async Task<IActionResult> AddPhotoAsync(int id, string photoUrl)
    {
        var client = await _service.GetClientAsync(Context);
        client.ReadResponseAsString = true;

        var result = await client.CreateProjectPhotoAsync(null, id.ToString(), new Body5
        {
            Photo = new Photo2
            {
                Uri = photoUrl,
                Captured_at = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                // Coordinates =
                Tags = new List<string>()
                {
                    "Photo",
                    "Test",
                    "Door"
                }
            }
        });

        return Ok(result);
    }

    [Authorize("admin")]
    [HttpPost("/companycam/v1/[controller]({id})/Document")]
    public async Task<IActionResult> AddDocumentAsync(int id, string name, string documentUrl)
    {
        var client = await _service.GetClientAsync(Context);
        client.ReadResponseAsString = true;

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, documentUrl);
        var response = await Client.SendAsync(requestMessage);
        var body = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
        {
            return BadRequest(response.StatusCode);
        }

        var base64String = Convert.ToBase64String(body);

        var result = await client.CreateProjectDocumentAsync(null, id.ToString(), new Body8
        {
            Document = new Document2
            {
                Name = name,
                Attachment = base64String,
            }
        });

        return Ok(result);
    }
}