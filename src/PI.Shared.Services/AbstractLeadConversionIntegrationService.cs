using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PI.Shared.Constants;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Services;

public abstract class AbstractLeadConversionIntegrationService : ILeadConversionIntegrationService
{
    protected readonly ILogger<AbstractLeadConversionIntegrationService> _logger;
    protected readonly MongoConnection _connection;
    protected readonly AuthorizationService _authorizationService;
    protected readonly ObjectTypeService _objectTypeService;
    protected readonly IConfiguration _configuration;

    public abstract Guid IntegrationId { get; }
    public abstract string ClientId { get; }

    public abstract Task<IResult> ConditionallyPostLeadAsync(Lead lead);

    protected AbstractLeadConversionIntegrationService(
        ILogger<AbstractLeadConversionIntegrationService> logger,
        MongoConnection connection,
        AuthorizationService authorizationService,
        ObjectTypeService objectTypeService,
        IConfiguration configuration
    )
    {
        this._logger = logger;
        this._connection = connection;
        this._authorizationService = authorizationService;
        this._objectTypeService = objectTypeService;
        this._configuration = configuration;
    }

    public async Task<Guid> AddNoteAsync(IEntityContext context, Guid leadId, string subject, string content, ContentFormat format)
    {
        var lead = await _connection.Filter<Lead>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, leadId)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q.Eq(x => x.IntegrationId, IntegrationId)
            )
            .FirstOrDefaultAsync();

        if (lead == null) throw NotFoundException.New<Lead>(leadId);

        return await AddNoteAsync(context, lead, subject, content, format);
    }

    public async Task<Guid> AddNoteAsync(IEntityContext context, Lead lead, string subject, string content = null, ContentFormat? format = null)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = context.Actor(),
            AccountId = context.AccountId.Value,
            EntityId = context.AccountId.Value, // TODO: have an user associated with the lumin client?
            Name = subject,
            // Description = // make it be the plain text representation of content
            Content = content,
            // ContentFormat = format,
            ContentType = format switch
            {
                ContentFormat.Html => "text/html",
                ContentFormat.PlainText => "text/plain",
                ContentFormat.Markdown => "text/markdown",
                _ => null
            },
            Refs = new List<KeyValuePair<string, object>>
            {
                new($"{nameof(Lead)}Id", lead.Id),
                new($"{nameof(Integration)}Id", IntegrationId),
            },
        };

        return await AddAsync(context, lead, note);
    }

    protected async Task<Guid> AddAsync(IEntityContext context, Lead lead, Note note)
    {
        var objectType = await _objectTypeService.GetAsync(context, note.GetType().Name);

        note.FlowId = objectType?.InitialFlowId;
        note.ObjectStatusId = objectType?.InitialObjectStatusId;

        note = await _connection.InsertAsync(note);

        var name = IntegrationIds.GetName(IntegrationId);
        await _objectTypeService.FireCreateEventAsync(context, note, e =>
        {
            e.Description ??= note.Name;
            e.Action ??= "ObjectCreated";

            e.SetRefValue(nameof(Lead), lead.Id);
            e.SetMetaValue(nameof(Lead), lead.Name);

            e.SetRefValue(nameof(Integration), IntegrationId);
            e.SetMetaValue(nameof(Integration), name);
        });

        return note.Id;
    }

    protected string CreateAuthorizationToken(Lead lead, int days = 30)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.ClientId, ClientId),
            new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
            new Claim("client_account_id", PI.Shared.Constants.AccountIds.FCI.ToString()),
            new Claim("pi_lead_id", lead.Id.ToString()),
            new Claim("pi_org_id", lead.EntityId.ToString()),
            new Claim("scope", "partner"),
            new Claim("scope", "scheduler"),
        };

        var authorization = _authorizationService.GenerateJwtToken(claims, TimeSpan.FromDays(days));
        if (!authorization)
        {
            throw new Exception("Failed to generate authorization for lead");
        }

        return authorization.Value;
    }

    protected async Task<Lead> SetCancelledOnAsync(Lead lead, string status)
    {
        var result = await _connection.Filter<PI.Shared.Models.Lead>()
            .Eq(x => x.AccountId, lead.AccountId)
            .Eq(x => x.Id, lead.Id)
            .ElemMatchBuilder(
                x => x.Integrations,
                q => q
                    .Eq(x => x.IntegrationId, IntegrationId)
                    .Eq(nameof(IExternalLeadIntegration.CancelledOn), default(DateTime?))
            )
            .Update
            .Set($"{nameof(PI.Shared.Models.Lead.Integrations)}.$.{nameof(IExternalLeadIntegration.CancelledOn)}", DateTime.UtcNow)
            .Set($"{nameof(PI.Shared.Models.Lead.Integrations)}.$.{nameof(IExternalLeadIntegration.Status)}", status)
            // .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            // .Set(x => x.LastActor, context.Actor())
            .UpdateAndGetOneAsync();

        return lead;
    }
}