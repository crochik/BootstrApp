using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using PI.Shared.Models.OpenAPI;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Models.Files;
using PI.Shared.Requests;
using PI.Shared.Services;
using PI.Shared.Services.OpenApiGenerator;

namespace PI.OpenAPI.Controllers;

[Route("/openapi/v1/[controller]")]
public class DocumentController : APIController
{
    private readonly MongoConnection _connection;
    private readonly RemoteFileService _remoteFileService;

    public DocumentController(MongoConnection connection, RemoteFileService remoteFileService)
    {
        _connection = connection;
        _remoteFileService = remoteFileService;
    }

    [Authorize("admin")]
    [HttpPost("DataForm")]
    public async Task<DataFormActionResponse> ExecImportActionAsync([FromServices] OpenApiParser parser, [FromBody] DataFormActionRequest<ImportDocumentRequest> request)
    {
        // TODO: should check that the file is where we expect to prevent someone from accessing a file that is not expected
        // ....
        var document = await _connection.InsertAsync(new Document
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            Description = request.Parameters.Description,
            RemoteFileId = request.Parameters.File,
            Namespace = request.Parameters.Namespace,
            // BaseUrl =
            // IntegrationId =
        });

        // it will most like timeout
        await ProcessAsync(document, parser);

        return DataFormActionResponse.Error(request, "Not yet");
    }

    [Authorize("admin")]
    [HttpPut("/openapi/v1/[controller]({id})/Load")]
    public async Task<IActionResult> ProcessAsync([FromRoute] Guid id, [FromServices] OpenApiParser parser)
    {
        var document = await _connection.Filter<Document>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        await ProcessAsync(document, parser);

        return Ok(parser.Diagnostic);
    }

    private async Task ProcessAsync(Document document, OpenApiParser parser)
    {
        parser.Namespace = document.Namespace;
        parser.AccountId = document.AccountId;
        parser.EntityId = document.EntityId;

        // TODO: should check that the file is where we expect to prevent someone from accessing a file that is not expected
        // ....

        var remoteFile = await _connection.Filter<RemoteFile>()
            .Eq(x => x.AccountId, document.AccountId)
            .Eq(x => x.Id, document.RemoteFileId.Value)
            .FirstOrDefaultAsync();

        var stream = await _remoteFileService.GetStreamAsync(Context, remoteFile);

        var result = await parser.LoadAsync(stream);
        
        result
            .ParseSchemas()
            .ParseResponses()
            .ParsePaths()
            // .ParseOperation("/projects", "createProject")
            .ResolveMissingLinks();

        var existing = (await _connection.Filter<Schema>()
                .Eq(x => x.AccountId, parser.AccountId)
                .Regex(x => x.Namespace, new BsonRegularExpression($"^{parser.Namespace}"))
                .FindAsync())
            .ToDictionary(x => x.FullName);

        // var dict = new Dictionary<string, object>();

        foreach (var ot in parser.ObjectTypes)
        {
            // dict.Add(ot.Value.ObjectType.FullName, ot.Value);

            if (existing.TryGetValue(ot.Value.ObjectType.FullName, out var current))
            {
                ot.Value.ObjectType.Id = current.Id;

                // TODO: merge to preserve customizations
                // ...
            }

            await _connection.Filter<Schema>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.FullName, ot.Value.FullName)
                .ReplaceOneAsync(ot.Value, true);

            await _connection.Filter<ObjectType>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                // .Eq(x => x.EntityId, Context.AccountId.Value)
                .Eq(x => x.FullName, ot.Value.ObjectType.FullName)
                .ReplaceOneAsync(ot.Value.ObjectType, true);
        }

        foreach (var operation in parser.Operations.Values)
        {
            await _connection.Filter<Operation>()
                .Eq(x => x.AccountId, operation.AccountId)
                .Eq(x => x.Namespace, operation.Namespace)
                .Eq(x => x.OperationId, operation.OperationId)
                .Update
                .SetOnInsert(x => x.Id, operation.Id)
                .SetOnInsert(x => x.AccountId, operation.AccountId)
                .SetOnInsert(x => x.OperationId, operation.OperationId)
                .SetOnInsert(x => x.CreatedOn, operation.CreatedOn)
                .SetOnInsert(x => x.Name, operation.Name)
                .SetOnInsert(x => x.Namespace, operation.Namespace)
                .SetOrUnset(x => x.Summary, operation.Summary)
                .SetOrUnset(x => x.Request, operation.Request)
                .SetOrUnset(x => x.Responses, operation.Responses)
                .SetOrUnset(x => x.Raw, operation.Raw)
                .AddToSetEach(x => x.Tags, operation.Tags)
                .UpdateOneAsync(true);
        }

        // foreach (var kvp in parser.Paths)
        // {
        //     dict.Add(kvp.Value.Name, kvp.Value);
        // }

        // dict.Add("_Diagnostic_", parser.Diagnostic);

        // var body = JsonConvert.SerializeObject(dict, JsonSerializerSettings);
        // await _connection.GetCollection<OpenApiPathItem>("Test").InsertOneAsync(obj);

        // return Content(body, "application/json");
    }

    public class ImportDocumentRequest
    {
        public string Description { get; set; }
        public string Namespace { get; set; }
        public Guid File { get; set; }
    }
}