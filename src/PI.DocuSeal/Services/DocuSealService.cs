using Microsoft.Extensions.Options;
using Crochik.Mongo;
using DocuSeal.Api.Clients;
using DocuSeal.Api.Models;
using PI.DocuSeal.Models;
using PI.DocuSeal.Providers;
using PI.Shared.Models;
using PI.Shared.Services;
using Providers;

namespace PI.DocuSeal.Services;

public class DocuSealService(
    ILogger<DocuSealService> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService,
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory clientFactory,
    IOptions<DocuSealConfiguration> config
)
{
    public HttpClient Client
    {
        get
        {
            var client = clientFactory.CreateClient(nameof(DocuSealService));
            client.BaseAddress = new Uri(config.Value.ApiUrl);
            client.DefaultRequestHeaders.Add("X-Auth-Token", config.Value.ApiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            return client;
        }
    }
    
    private async Task<Result<EmbeddedContent>> GenerateDocumentAsync(IEntityContext context, Guid templateId, IDictionary<string, object> runContext)
    {
        // TODO: limit templates by org?
        // .... 
        var documentTemplate = await connection.Filter<DocumentTemplate>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            // .Eq(x => x.EntityId, Context.EntityId.Value)
            .Eq(x => x.Id, templateId)
            .FirstOrDefaultAsync();

        if (documentTemplate == null) return Result.Error<EmbeddedContent>("Invalid or Missing Template");

        return await GenerateDocumentAsync(context, documentTemplate, runContext);
    }

    public async Task<Result<IDictionary<string, object>>> BuildObjectContextAsync(IEntityContext context, string objectTypeName, Guid objectId)
    {
        var objectType = await objectTypeService.GetAsync(context, objectTypeName);
        if (objectType == null) return Result.Error<IDictionary<string, object>>("Object type not found");

        var obj = await objectTypeService.GetFlatObjectAsync(context, objectType, objectId);
        // var obj = await objectTypeService.GetExpandoObjectByIdAsync(context, objectType, objectId);
        if (obj == null) return Result.Error<IDictionary<string, object>>("Object not found");

        return Result.Success<IDictionary<string, object>>(new Dictionary<string, object>
        {
            { "Object", obj }
        });
    }

    public async Task<Result<EmbeddedContent>> GenerateDocumentAsync(IEntityContext context, string objectTypeName, Guid objectId, DocumentTemplate documentTemplate)
    {
        var docContext = await BuildObjectContextAsync(context, objectTypeName, objectId);
        if (docContext.IsError) return docContext.ConvertTo<EmbeddedContent>();

        return await GenerateDocumentAsync(context, documentTemplate, docContext.Value);
    }

    public async Task<Result<EmbeddedContent>> GenerateDocumentAsync(IEntityContext context, DocumentTemplate documentTemplate, IDictionary<string, object> objectContext)
    {
        // TODO: check template content type?
        // ...

        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ITemplateProvider>();
        if (provider is HandlebarsTemplateProvider handlebarsProvider)
        {
            RegisterHelpers(handlebarsProvider);
        }
                
        var result = await provider.RenderTemplateAsync(context, documentTemplate, objectContext);

        if (result == null) return Result.Error<EmbeddedContent>("Generated empty document");

        return Result.Success(new EmbeddedContent
        {
            Content = result,
            ContentType = documentTemplate.ContentType,
            Size = result.Length,
        });
    }

    public static void RegisterHelpers(HandlebarsTemplateProvider handlebarsProvider)
    {
        handlebarsProvider.RegisterHelper("html:customer-signature", (writer, context, args) =>
        {
            writer.Write("<signature-field name=\"Customer's Signature\" role=\"Customer\"style=\"width: 160px; height: 80px; display: inline-block;\"></signature-field>", false);
        });
            
        handlebarsProvider.RegisterHelper("html:customer-signature-date", (writer, context, args) =>
        {
            writer.Write("<date-field name=\"Date\" role=\"Customer\" style=\"width: 100px; height: 25px; display: inline-block;\"></date-field>", false);
        });
        handlebarsProvider.RegisterHelper("html:div", (writer, options, context, args) =>
        {
            var className = "section";
            if (args.Length == 1 && args[0] is string str)
            {
                className = str;
            }

            writer.Write($"<div class=\"{className}\">", encode: false);
            options.Template(writer, context);
            writer.Write("</div>", encode: false);
        });
    }

    public async Task<Result<DocuSealSubmission>> CreateSubmissionAsync(IEntityContext context, DocuSealSubmission submission, IDictionary<string, object> runContext)
    {
        if (string.IsNullOrEmpty(submission.Content))
        {
            var result = await GenerateDocumentAsync(context, submission.TemplateId, runContext);
            if (result.IsError) return result.ConvertTo<DocuSealSubmission>();
            if (result.Value.ContentType != "text/html") return Result.Error<DocuSealSubmission>("Unexpected content type");

            submission.Content = result.Value.Content;
            submission.ContentType = result.Value.ContentType;
        }
        
        var objectType = await objectTypeService.GetAsync(context, DocuSealSubmission.ObjectTypeFullName);

        submission.Id = Guid.CreateVersion7();
        submission.AccountId = context.AccountId.Value;
        submission.EntityId = context.OrganizationId ?? context.UserId.Value; // owned by org or user (for admins)
        submission.CreatorId = context.UserId.Value;
        submission.CreatedOn = DateTime.UtcNow;
        submission.LastActor = context.Actor();
        submission.FlowId = objectType?.InitialFlowId;
        submission.ObjectStatusId = objectType?.InitialObjectStatusId;
        submission.IsActive = false;
        // submission.ExternalId = "TBD"

        submission = await objectTypeService.InsertAsync(context, submission);

        return await SubmitAsync(context, submission);
    }

    private async Task<Result<DocuSealSubmission>> SubmitAsync(IEntityContext context, DocuSealSubmission submission)
    {
        if (string.IsNullOrEmpty(submission.Content)) return Result.Error<DocuSealSubmission>("Empty submission");
        if (submission.ContentType != "text/html") return Result.Error<DocuSealSubmission>("Only HTML submissions right now");

        try
        {
            var response = await new Submissions(Client)
                .CreateSubmissionFromHtmlAsync(new CreateSubmissionFromHtmlRequest
                {
                    Name = submission.Name,
                    SendEmail = false,
                    SendSms = false,
                    Documents =
                    [
                        new CreateSubmissionFromHtmlRequestDocumentsItem
                        {
                            Html = submission.Content,
                        }
                    ],
                    Submitters = submission.Submitters
                        .Select(x => new CreateSubmissionFromHtmlRequestSubmittersItem
                        {
                            Name = x.Name,
                            Email = x.Email,
                            Role = x.Role,
                            ExternalId = x.Id ?? x.Name,
                            SendEmail = false,
                            SendSms = false,
                        })
                        .ToList(),
                });

            submission = await connection.Filter<DocuSealSubmission>()
                .Eq(x => x.AccountId, submission.AccountId)
                .Eq(x => x.Id, submission.Id)
                .Update
                .Set(x => x.ExternalId, response.Id)
                .Set(x => x.Submitters, response.Submitters.Select(x => new DocuSealSubmitter
                    {
                        Name = x.Name,
                        Email = x.Email,
                        Role = x.Role,
                        Id = x.ExternalId,
                        ExternalId = x.Id,
                        Slug = x.Slug,
                    })
                    .ToArray()
                )
                .Set(x => x.IsActive, true)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateAndGetOneAsync();

            await objectTypeService.FireObjectUpdatedAsync(context, submission, new Dictionary<string, object>
            {
                { nameof(DocuSealSubmission.ExternalId), submission.ExternalId },
                { nameof(DocuSealSubmission.IsActive), submission.IsActive },
                { nameof(DocuSealSubmission.Submitters), "*" },
            });

            return Result.Success(submission);
        }
        catch (Exception ex)
        {
            // TODO: update submission object
            // ...

            logger.LogError(ex, "Failed to create submission");
            return Result.Error<DocuSealSubmission>(ex.Message);
        }
    }

    public string CreateHtml(DocuSealSubmission? submission)
    {
        var slug = submission?.Submitters?.FirstOrDefault()?.Slug;
        if (slug == null) return "<html><body>This is not a valid URL</body></html>";
        
        return $@"
<html>
    <head>
<title>{submission.Name}</title>
<script src=""https://cdn.docuseal.com/js/form.js""></script>
</head>
<body>
<docuseal-form
" + $"data-src=\"https://docuseal.com/s/{slug}\">\n" + @"
</docuseal-form>
</body>
</html>
                ";
    }
    
    public string CreateHtml(String slug)
    {
        return @"
<html>
    <head>
<title>Test</title>
<script src=""https://cdn.docuseal.com/js/form.js""></script>
</head>
<body>
<docuseal-form
" + $"data-src=\"https://docuseal.com/s/{slug}\">\n" + @"
</docuseal-form>
</body>
</html>
                ";
    }
}