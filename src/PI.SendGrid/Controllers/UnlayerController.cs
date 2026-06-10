using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using HandlebarsDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PI.Shared.Controllers;
using PI.Shared.Data.Models;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Email;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;

namespace Controllers;

[Route("/sendgrid/v1/[controller]")]
public class UnlayerController : APIController
{
    private readonly MongoConnection _connection;
    private readonly SendGridEmailService _emailService;
    private readonly ObjectTypeService _objectTypeService;
    private readonly UnlayerConfig _configuration;

    public UnlayerController(
        IConfiguration configuration,
        MongoConnection connection,
        SendGridEmailService emailService,
        ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _emailService = emailService;
        _objectTypeService = objectTypeService;
        _configuration = configuration.GetSection("Unlayer").Get<UnlayerConfig>();
    }

    [Authorize("default")]
    [HttpGet]
    public async Task<UnlayerSession> GetAsync([FromQuery] string templateType, Guid? templatedObjectId)
    {
        var key = Encoding.ASCII.GetBytes(_configuration.Secret);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(Context.UserId.Value.ToString()));
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();

        return new UnlayerSession
        {
            ProjectId = _configuration.ProjectId,
            UserId = Context.UserId.Value.ToString(),
            Signature = hexHash,
            MergeTags = await GetMergeTagsAsync(templateType, templatedObjectId),
        };
    }

    private async Task<UnlayerMergeTagGroup[]> GetMergeTagsAsync(string templateType, Guid? templatedObjectId)
    {
        var special = templateType switch
        {
            nameof(BulkEmail) => await GetMergeTagsForSnapshotAsync(templatedObjectId),
            nameof(Snapshot) => await GetMergeTagsForSnapshotAsync(templatedObjectId),
            $"{nameof(Snapshot)}|{nameof(AppDataView)}" => await GetMergeTagsForAppDataViewInSnapshotAsync(templatedObjectId),
            _ => null,
        };

        return enumerate()
            .Where(x => x != null)
            .ToArray();

        IEnumerable<UnlayerMergeTagGroup> enumerate()
        {
            yield return special;
            yield return GetDefaultMergeTags(templateType);
        }
    }

    private async Task<UnlayerMergeTagGroup> GetMergeTagsForAppDataViewInSnapshotAsync(Guid? templatedObjectId)
    {
        if (!templatedObjectId.HasValue) return null;

        var appDataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, templatedObjectId.Value)
            // .Eq(x=>x.CreatedById, Context.UserId.Value)
            .FirstOrDefaultAsync();

        if (appDataView == null) return null;

        var objectType = await _objectTypeService.GetAsync(Context, appDataView.ObjectType);
        if (objectType == null) return null;

        // Add "Properties." prefix to the properties as they will get it when converted into a snapshot 
        return new UnlayerMergeTagGroup
        {
            Name = appDataView.Name,
            MergeTags = appDataView.Fields.ToDictionary(x => x, x => UnlayerMergeTag.New(objectType.Fields.TryGetValue(x, out var field) ? field.Field.Label ?? field.Field.Name : x, "{{" + FormField.GetPathInCollection($"Properties|{x}") + "}}"))
        };
    }

    private async Task<UnlayerMergeTagGroup> GetMergeTagsForSnapshotAsync(Guid? templatedObjectId)
    {
        if (!templatedObjectId.HasValue) return null;

        var snapshot = await _connection.Filter<Snapshot>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, templatedObjectId.Value)
            // .Eq(x=>x.CreatedById, Context.UserId.Value)
            .FirstOrDefaultAsync();

        if (snapshot == null) return null;

        return new UnlayerMergeTagGroup
        {
            Name = snapshot.Name,
            MergeTags = snapshot.DataView.Fields.ToDictionary(x => x.Name, x => UnlayerMergeTag.New(x.Label ?? x.Name, "{{" + FormField.GetPathInCollection(x.Name) + "}}"))
        };
    }

    [Authorize("default")]
    [HttpPost("Upload")]
    [Consumes("application/octet-stream", "multipart/form-data")]
    [RequestSizeLimit(5_000_000)]
    public async Task<UnlayerImageUploadResponse> UploadFileAsync(IFormFile file, [FromServices] IFileStorageService storage)
    {
        // TODO: move into a service 
        // ...
        var remoteFile = new UnlayerRemoteFile
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            Name = file.FileName,
            Provider = storage.Provider,
            LastActor = Context.Actor(),
            CreatedOn = DateTime.UtcNow,
            Size = (int)file.Length,
            ContentType = file.ContentType,
        };

        var index = file.FileName.LastIndexOf('.');
        var fileExtension = index > 0 ? file.FileName[index..] : null;
        if (fileExtension == null)
        {
            index = file.ContentType.LastIndexOf('/');
            if (index > 0) fileExtension = "." + file.ContentType[++index..];
        }

        var path = $"{Context.AccountId.Value}/{remoteFile.Id:N}{fileExtension}";

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadAsync(stream, file.ContentType, _configuration.Bucket, path, true);

        remoteFile.Description = path;
        remoteFile.PublicUrl = $"{_configuration.PublicUrlPrefix}/{path}";
        remoteFile.Uri = url;

        // TODO: use objectType service, flows, status, fire events, ...
        await _connection.InsertAsync(remoteFile);

        return new UnlayerImageUploadResponse
        {
            Url = remoteFile.PublicUrl,
        };
    }

    [Authorize("default")]
    [HttpGet("/sendgrid/v1/[controller]({id})/SendTestEmail/DataForm")]
    public async Task<Form> GetFormToSaveDataView([FromRoute] Guid? id)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, Context.UserId.Value)
            .FirstOrDefaultAsync();

        var template = Request.Query["Template"].FirstOrDefault();

        var fields = defaultFields().Concat(appointmentFields()).ToArray();

        return new Form()
        {
            Name = "SnedTestEmail",
            Title = "Send Test Email",
            Fields = fields,
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Send"
                }
            }
        };

        IEnumerable<FormField> defaultFields()
        {
            yield return new EmailField
            {
                Name = "To",
                IsRequired = true,
                DefaultValue = user?.Email,
            };

            yield return new TextField
            {
                Name = "Subject",
                IsRequired = true,
                DefaultValue = "Test Email"
            };

            yield return new HiddenField
            {
                Name = "Template",
                DefaultValue = template,
            };
        }

        IEnumerable<FormField> appointmentFields()
        {
            yield return new TextField { Name = "LocalDate", Label = "Appointment Date", DefaultValue = DateTime.UtcNow.ToString("MM/dd/yyyy") }; // "{{Objects.Appointment.LocalDate}}") },
            yield return new TextField { Name = "LocalTime", Label = "Appointment Time", DefaultValue = DateTime.UtcNow.ToString("hh:mm tt") }; // "{{Objects.Appointment.LocalTime}}") },
            yield return new TextField { Name = "Name", Label = "Lead Full Name", DefaultValue = "John Doe" }; // "{{Objects.Appointment|LeadId.Name}}") },
            // yield return new TextField { Name = "FirstName", Label = "First Name", DefaultValue = "John" }; // "{{Objects.Appointment|LeadId.FirstName}}") },
            // yield return new TextField { Name = "LastName", Label = "Last Name", DefaultValue = "Doe" }; // "{{Objects.Appointment|LeadId.LastName}}") },
            // yield return new EmailField { Name = "NormalizedEmail", Label = "Email", DefaultValue = "john.doe@hotmail.com" }; // "{{Objects.Appointment|LeadId.NormalizedEmail}}") },
            yield return new TextField { Name = "UserName", Label = "Design Associate", DefaultValue = user.Name }; // "{{Objects.Appointment|EntityId.Name}}") },
            yield return new EmailField { Name = "UserEmail", Label = "DA Email", DefaultValue = user.Email }; // ""{{Objects.Appointment|EntityId.Email}}") },
            // yield return new TextField { Name =  "iCal", Label = "iCal Attachment", "{{Objects.Appointment|iCal}}") },
            // { "Organization", UnlayerMergeTag.New("Organization (Description)", "{{Objects.Appointment|LeadId|EntityId.Description}}") },
            // { "OrganizationEmail", UnlayerMergeTag.New("Organization Email", "{{Objects.Appointment|LeadId|EntityId.Email}}") },
        }
    }

    /// <summary>
    /// Template custom actions
    /// </summary>
    [Authorize("default")]
    [HttpPost("/sendgrid/v1/[controller]({id})/SendTestEmail/DataForm")]
    public async Task<DataFormActionResponse> RunActionAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest request)
    {
        if (!request.Parameters.TryGetStrParam("Template", out var templateType))
        {
            return new DataFormActionResponse(request, "Missing required 'template' field");
        }

        if (!request.Parameters.TryGetStrParam("To", out var to))
        {
            return new DataFormActionResponse(request, "Missing required 'to' field");
        }

        if (!request.Parameters.TryGetStrParam("Subject", out var subject))
        {
            return new DataFormActionResponse(request, "Missing required 'subject' field");
        }

        var template = await _connection.Filter<UnlayerTemplate>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (template == null)
        {
            return new DataFormActionResponse(request, "Invalid template");
        }

        var (data, auth) = await _emailService.GetIntegrationSettingsAsync(Context);
        if (string.IsNullOrEmpty(auth?.APIKey) || string.IsNullOrEmpty(data?.FromName) || string.IsNullOrEmpty(data?.FromEmail))
        {
            return new DataFormActionResponse(request, "Account not configured to send emails");
        }

        switch (templateType)
        {
            case "Appointment":
                return await SendAppointmentEmailAsync(template, data, auth, request, to, subject);
        }

        return new DataFormActionResponse(request, "Unknown template");
    }

    private async Task<DataFormActionResponse> SendAppointmentEmailAsync(UnlayerTemplate template, SendGridIntegration.Data data, SendGridIntegration.Authentication authentication, DataFormActionRequest request, string to, string subject)
    {
        var user = await _connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, Context.UserId.Value)
            .FirstOrDefaultAsync();

        var organization = default(Organization);
        if (user.OrganizationId.HasValue)
        {
            organization = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, user.OrganizationId ?? Guid.Empty)
                .FirstOrDefaultAsync();
        }

        organization ??= new Organization
        {
            Id = Guid.Empty,
            AccountId = Context.AccountId.Value,
            Name = "<<Organization>>",
            Description = "<<Organization Description>>",
            Email = "<<Organization Email>>",
        };

        var appointment = new Appointment
        {
            Id = Guid.Empty,
            AccountId = Context.AccountId.Value,
            LocalDate = request.Parameters["LocalDate"].ToString(),
            LocalTime = request.Parameters["LocalTime"].ToString(),
            TimeZoneId = "America/New_York",
        };

        var lead = new Lead
        {
            Id = Guid.Empty,
            AccountId = Context.AccountId.Value,
            Name = request.Parameters["Name"].ToString(),
        };

        // TODO: won't be needed after we merge lead first class props pr
        // ...
        lead.SetValue(Lead.PropertyName_Email, to);
        lead.SetValue(Lead.PropertyName_Name, lead.Name);

        var da = new User
        {
            Id = Guid.Empty,
            OrganizationId = organization.Id,
            AccountId = Context.AccountId.Value,
            Name = request.Parameters["UserName"].ToString(),
            Email = request.Parameters["UserEmail"].ToString(),
            UserRoleId = EntityRoleId.User.ToString(),
        };

        dynamic context = new ExpandoObject();
        context.InitialObject = appointment;
        context.Object = appointment;
        context.Objects = new Dictionary<string, object>
        {
            { "Appointment", appointment },
            { "Appointment|EntityId", da },
            { "Appointment|LeadId", lead },
            { "Appointment|LeadId|EntityId", organization },
        };

        var handlebarsContext = JsonConvert.DeserializeObject<ExpandoObject>(JsonConvert.SerializeObject(context));

        var emailMessage = new EmailMessage
        {
            From = new EmailAddress
            {
                Name = data.FromName,
                Email = data.FromEmail,
            },
            To = new[]
            {
                new EmailAddress
                {
                    Name = lead.Name,
                    Email = lead.NormalizedEmail,
                }
            },
            Subject = subject,
            HtmlBody = Handlebars.Compile(template.Html).Invoke(handlebarsContext),
        };

        var sendGridEmailMessage = new SendGridEmailMessage
        {
            Id = Model.NewObjectId(),
            CreatedOn = DateTime.UtcNow,
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            // FlowRunId = action.Event.RunId,
            // TriggerObjectType = action.Event.ObjectType,
            // TriggerObjectId = action.Event.TargetId,
            // FlowId = sendGridObjectType?.InitialFlowId,
            // ObjectStatusId = sendGridObjectType?.InitialObjectStatusId,
            Message = emailMessage,
        };

        var result = await _emailService.SendAsync(authentication.APIKey, sendGridEmailMessage);

        return new DataFormActionResponse(request, result.Status, result.IsSuccess)
        {
            Action = FormAction.Client_Cancel,
        };
    }

    private UnlayerMergeTagGroup GetDefaultMergeTags(string objectType)
    {
        return objectType switch
        {
            "AppointmentEvent::Default" => new UnlayerMergeTagGroup
            {
                Name = "Appointment",
                MergeTags = new Dictionary<string, UnlayerMergeTag>
                {
                    { "LocalDate", UnlayerMergeTag.New("Appointment Date", "{{Objects.Appointment.LocalDate}}") },
                    { "LocalTime", UnlayerMergeTag.New("Appointment Time", "{{Objects.Appointment.LocalTime}}") },
                    { "FirstName", UnlayerMergeTag.New("First Name", "{{Objects.Appointment|LeadId.FirstName}}") },
                    { "LastName", UnlayerMergeTag.New("Last Name", "{{Objects.Appointment|LeadId.LastName}}") },
                    { "Name", UnlayerMergeTag.New("Full Name", "{{Objects.Appointment|LeadId.Name}}") },
                    { "NormalizedEmail", UnlayerMergeTag.New("Email", "{{Objects.Appointment|LeadId.NormalizedEmail}}") },
                    { "UserEmail", UnlayerMergeTag.New("DA Email", "{{Objects.Appointment|EntityId.Email}}") },
                    { "UserName", UnlayerMergeTag.New("Design Associate", "{{Objects.Appointment|EntityId.Name}}") },
                    // { "iCal", UnlayerMergeTag.New("iCal Attachment", "{{Objects.Appointment|iCal}}") },
                    // { "Message", UnlayerMergeTag.New("Custom Message", "{{InitialEvent.MetaValues.Message}}") },
                    { "Organization", UnlayerMergeTag.New("Organization (Description)", "{{Objects.Appointment|LeadId|EntityId.Description}}") },
                    { "OrganizationEmail", UnlayerMergeTag.New("Organization Email", "{{Objects.Appointment|LeadId|EntityId.Email}}") },
                    { "OrganizationPhone", UnlayerMergeTag.New("Organization Phone", "{{Objects.Appointment|LeadId|EntityId.Phone}}") },
                    { "UnsubscribeLink", UnlayerMergeTag.New("Unsubscribe Link", "{{{Action.UnsubscribeLink}}}") },
                    { "EmailId", UnlayerMergeTag.New("Email Id", "{{Action.EmailId}}") },
                },
            },

            // nameof(Lead) => new UnlayerMergeTagGroup
            // {
            //     Name = "Lead",
            //     MergeTags = new Dictionary<string, UnlayerMergeTag>
            //     {
            //         { "FirstName", UnlayerMergeTag.New("First Name", "{{Objects.Lead.FirstName}}") },
            //         { "LastName", UnlayerMergeTag.New("Last Name", "{{Objects.Lead.LastName}}") },
            //         { "Name", UnlayerMergeTag.New("Full Name", "{{Objects.Lead.Name}}") },
            //         { "NormalizedEmail", UnlayerMergeTag.New("Email", "{{Objects.Lead.NormalizedEmail}}") },
            //         { "Message", UnlayerMergeTag.New("Custom Message", "{{InitialEvent.MetaValues.Message}}") },
            //     },
            // },

            _ => new UnlayerMergeTagGroup
            {
                Name = "Email",
                MergeTags = new Dictionary<string, UnlayerMergeTag>
                {
                    { "UnsubscribeLink", UnlayerMergeTag.New("Unsubscribe Link", "{{{Action.UnsubscribeLink}}}") },
                    { "EmailId", UnlayerMergeTag.New("Email Id", "{{Action.EmailId}}") },
                },
            },
        };
    }
}

public class UnlayerMergeTagGroup
{
    public string Name { get; set; }
    public Dictionary<string, UnlayerMergeTag> MergeTags { get; set; }
}

public class UnlayerMergeTag
{
    public static UnlayerMergeTag New(string name, string value) => new UnlayerMergeTag
    {
        Name = name,
        Value = value,
    };

    public string Name { get; set; }
    public string Value { get; set; }
}

public class UnlayerSession
{
    public int ProjectId { get; set; }
    public string UserId { get; set; }
    public string Signature { get; set; }
    public UnlayerMergeTagGroup[] MergeTags { get; set; }
}

public class UnlayerConfig
{
    public int ProjectId { get; set; }
    public string Secret { get; set; }
    public string Bucket { get; set; }
    public string PublicUrlPrefix { get; set; }
}

public class UnlayerImageUploadResponse
{
    public string Url { get; set; }
}