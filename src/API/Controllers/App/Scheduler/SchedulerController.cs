using System.Dynamic;
using System.Security.Claims;
using AutoMapper;
using Controllers.Models;
using Crochik.Logging;
using Crochik.Mongo;
using IdentityModel;
using LMS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PI.Shared.Constants;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Models;
using PI.Shared.Models.GeoJSON;
using PI.Shared.Models.U2;
using PI.Shared.Services;
using AppointmentIntegration = PI.Shared.Models.AppointmentIntegration;
using Lead = Controllers.Models.Lead;
using LeadType = PI.Shared.Data.Models.LeadType;
using Organization = PI.Shared.Models.Organization;

namespace Controllers.Scheduler;

[ApiExplorerSettings(GroupName = "scheduler")]
[Produces("application/json")]
[Route("/app/[controller]")]
public class SchedulerController : APIController
{
    /// <summary>
    /// Get Lead Id from the JWT token
    /// </summary>
    private Guid LeadId
    {
        get
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == "pi_lead_id");
            if (claim == null || !Guid.TryParse(claim.Value, out var leadId))
            {
                throw new ForbiddenException(Context, "Missing Lead");
            }

            return leadId;
        }
    }

    private readonly ILogger<SchedulerController> _logger;
    private readonly IMapper _mapper;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private readonly LeadBuilderService _leadBuilderService;
    private readonly AppointmentSchedulerService _scheduler;

    public SchedulerController(
        ILogger<SchedulerController> logger,
        IMapper mapper,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        LeadBuilderService leadBuilderService,
        AppointmentSchedulerService scheduler
    )
    {
        _logger = logger;
        _mapper = mapper;
        _connection = connection;
        _objectTypeService = objectTypeService;
        _leadBuilderService = leadBuilderService;
        _scheduler = scheduler;
    }

    [AllowAnonymous]
    [HttpGet("{leadId}")]
    public async Task<IActionResult> RedirectAsync([FromRoute] string leadId, [FromServices] AuthorizationService authorizationService)
    {
        using var scope = _logger.AddScope(new
        {
            LeadId = leadId,
        });

        var (baseUrl, auth) = await GetRedirectionAsync(leadId, authorizationService);

        var url = $"{baseUrl}/#/existing/";

        return Redirect($"{url}{auth}");
    }

    [AllowAnonymous]
    [HttpGet("{shortCode}/{leadId}")]
    public async Task<IActionResult> RedirectUsingShortCodeAsync([FromRoute] string shortCode, [FromRoute] string leadId, [FromServices] AuthorizationService authorizationService)
    {
        using var scope = _logger.AddScope(new
        {
            Host = Request.Host.Value,
            ShortCode = shortCode,
            LeadId = leadId,
        });

        var redirection = await _connection.Filter<ShortLinkRedirection>()
            .Eq(x => x.Host, Request.Host.Value)
            .Eq(x => x.ShortCode, shortCode)
            .Eq(x => x.IsActive, true)
            .Update
            .Inc(x => x.ViewCount, 1)
            .Set(x => x.LastAccessedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (redirection == null)
        {
            _logger.LogError("Redirection not configured, use default");
            return await RedirectAsync(leadId, authorizationService);
        }

        var (baseUrl, auth) = await GetRedirectionAsync(leadId, authorizationService, shortCode);

        var location = redirection.Location;
        location = location.Replace("{{auth}}", auth);
        location = location.Replace("{{baseUrl}}", baseUrl);

        await _connection.InsertAsync(new RedirectionRequest
        {
            RedirectionId = redirection.Id,
            UserAgent = Request.Headers["User-Agent"],
            IpAddress = Request.Headers["X-Real-IP"],
            RequestId = Request.Headers["X-Request-ID"],
            Location = location,
            Url = Request.GetDisplayUrl(),
            Query = Request.Query?.ToDictionary(x => x.Key, x => (object)(x.Value.Count == 1 ? x.Value.First() : x.Value.ToArray())),
        });

        return Redirect(location);
    }

    private async Task<(string BaseUrl, string Auth)> GetRedirectionAsync(string leadOrExternalId, AuthorizationService authorizationService, string shortCode = null)
    {
        var lead = default(PI.Shared.Models.Lead);
        if (!Guid.TryParse(leadOrExternalId, out var leadId))
        {
            // external Id
            lead = await _connection.Filter<PI.Shared.Models.Lead>()
                .ElemMatchBuilder(x => x.Integrations, q => q.Eq(x => x.ExternalId, leadOrExternalId))
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }
        else
        {
            // id 
            lead = await _connection.Filter<PI.Shared.Models.Lead>().Eq(x => x.Id, leadId)
                .Ne(x => x.IsActive, false)
                .FirstOrDefaultAsync();
        }

        if (lead == null)
        {
            _logger.LogError("Lead not found");
            throw NotFoundException.New<PI.Shared.Models.Lead>(leadId);
        }

        var account = await _connection.Filter<Entity>()
            .Eq(x => x.Id, lead.AccountId)
            .IncludeField(x => x.Integrations)
            .FirstOrDefaultAsync();

        var integration = account?.Integrations?
            .OfType<SchedulerEntityIntegration>()
            .FirstOrDefault();

        if (integration == null)
        {
            _logger.LogError("Trying to launch scheduler for {AccountId} that is not configured for it", lead.AccountId);
            throw new NotFoundException();
        }

        _logger.LogInformation("Using {ClientId} and {BaseUrl} from account integration", integration.ClientId, integration.BaseUrl);
        var clientId = integration.ClientId;
        var baseUrl = integration.BaseUrl;

        if (shortCode != null && integration.AlternativeClientIds?.FirstOrDefault(x => x == shortCode) != null)
        {
            // var client = await _connection.Filter<AppClient>()
            //     .Eq(x => x.AccountId, lead.AccountId)
            //     .Eq(x => x.ClientId, shortCode)
            //     .Eq(x => x.Enabled, true)
            //     .Ne(x => x.ClientUri, null)
            //     .FirstOrDefaultAsync();
            //
            // if (client != null)
            // {
            //     _logger.LogInformation("Found {ClientId} with {ClientUri}", client.ClientId, client.ClientUri);
            //     clientId = client.ClientId;
            //     baseUrl = client.ClientUri;
            // }

            clientId = shortCode;
        }


        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.ClientId, clientId),
            new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString()),
            new Claim("client_account_id", lead.AccountId.ToString()),
            new Claim("pi_lead_id", lead.Id.ToString()),
            new Claim("pi_org_id", lead.EntityId.ToString()),
            new Claim("scope", "client_app"),
            new Claim("scope", "scheduler"),
        };

        if (Request.QueryString.HasValue && !string.IsNullOrWhiteSpace(Request.QueryString.Value) && Request.QueryString.Value.Length < 1024)
        {
            claims.Add(new Claim("client_query_string", Request.QueryString.Value));
        }

        var authorization = authorizationService.GenerateJwtToken(claims, TimeSpan.FromHours(4));
        if (!authorization)
        {
            throw new Exception("Failed to generate authorization for lead");
        }

        return (baseUrl, authorization.Value);
    }

    [Authorize("schedulerExisting")]
    [HttpPost("Session/Lead")]
    public async Task<SessionConfig> InitSessionForLeadAsync()
    {
        using var scope = _logger.AddScope(new
        {
            Context.ClientId,
            LeadId,
        });

        _logger.LogInformation("Init Session");

        // TODO: should check if there is already any other session for the same jti
        // the CreateSessionForLeadAsync doesn't enforce it like the others
        // ...

        var session = await _scheduler.CreateSessionForLeadAsync(
            Context,
            LeadId,
            name: $"{Context.ClientId}_{LeadId}",
            referer: Request.Headers.Referer.FirstOrDefault(),
            externalId: ((AbstractAPIActor)Context.Actor).TokenId
        );

        return await MapAsync(session);
    }

    [Obsolete("the notes should be added/updted as part of the lead")]
    [Authorize("scheduler")]
    [HttpPost("/app/[controller]({sessionId})/Note")]
    public async Task AddNoteAsync([FromBody] NoteReq request, [FromRoute] Guid sessionId)
    {
        var session = await _scheduler.GetExistingSession(Context, sessionId);
        if (!session.LeadId.HasValue) throw new BadRequestException("No Lead");

        // if (session.AppointmentId.HasValue)
        // {
        //     if (session.Appointment?.Id != session.AppointmentId)
        //     {
        //         session.Appointment = await _connection.Filter<PI.Shared.Models.Appointment>()
        //             .Eq(x => x.AccountId, Context.AccountId.Value)
        //             .Eq(x => x.Id, session.AppointmentId.Value)
        //             .FirstOrDefaultAsync();
        //     }
        //
        //     if (session.Appointment == null) throw NotFoundException.New<PI.Shared.Models.Appointment>(session.AppointmentId.Value);
        //     if (session.Appointment.IsActive && session.Appointment.Start > DateTime.UtcNow)
        //     {
        //         await AddNoteAsync(request, session.Appointment);
        //         return;
        //     }
        // }

        if (session.Lead?.Id != session.LeadId)
        {
            session.Lead = await _connection.Filter<PI.Shared.Models.Lead>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.Id, session.LeadId.Value)
                .FirstOrDefaultAsync();
        }

        if (session.Lead == null) throw NotFoundException.New<PI.Shared.Models.Appointment>(session.LeadId.Value);

        await AddNoteAsync(request, session.Lead);
    }

    [Authorize("scheduler")]
    [HttpGet("/app/[controller]({sessionId})/Organization")]
    public async Task<OrganizationResp> GetOrganizationForSessionAsync([FromRoute] Guid sessionId)
    {
        using var scope = _logger.AddScope(new
        {
        });

        _logger.LogInformation("Get Organization");

        var session = await _scheduler.GetExistingSession(Context, sessionId);
        return await GetOrganizationAsync(session);
    }

    /**
     * Validate whether a postal code belongs to the organization
     */
    [Authorize("scheduler")]
    [HttpGet("/app/[controller]({sessionId})/PostalCode({postalCode})/Validate")]
    public async Task<ValidatePostalCodeResp> ValidatePostalCodeForSessionAsync([FromRoute] Guid sessionId, [FromRoute] string postalCode)
    {
        var session = await _scheduler.GetExistingSession(Context, sessionId);
        var organizationId = session.EntityId;

        postalCode = PI.Shared.Models.Lead.GetPostalCodeForLookup(postalCode);
        if (postalCode == null)
        {
            _logger.LogInformation("{PostalCode} is not valid", postalCode);
            return new ValidatePostalCodeResp
            {
                IsValid = false,
                Error = "Invalid Postal Code",
            };
        }

        var territory = await _connection.Filter<CustomObject>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.ObjectType, "ZeeTerritory")
            .Eq(x => x.ExternalId, postalCode)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (territory == null)
        {
            _logger.LogInformation("{PostalCode} is not serviced by any org", postalCode);
            return new ValidatePostalCodeResp
            {
                IsValid = false,
                Error = "Not serviced",
            };
        }

        if (territory.EntityId == organizationId)
        {
            _logger.LogInformation("{PostalCode} is valid for current {SessionId}/{OrganizationId}", postalCode, sessionId, session.EntityId);
            return new ValidatePostalCodeResp
            {
                IsValid = true,
            };
        }

        // check whether the org ia active
        var settingsForOtherOrg = await _connection.Filter<SchedulerSettings>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .In(x => x.ClientId, [null, Context.ClientId])
            .Eq(x => x.EntityId, territory.EntityId)
            .SortDesc(x => x.ClientId) // prefer match to client, null is a fallback
            .FirstOrDefaultAsync();

        if (settingsForOtherOrg == null || !settingsForOtherOrg.IsActive)
        {
            _logger.LogInformation("{PostalCode}: {SchedulerSettingsId} for {EntityId}: {OutOfServiceMessage} ", postalCode, settingsForOtherOrg?.Id, territory.EntityId, settingsForOtherOrg?.OutOfServiceMessage);
            return new ValidatePostalCodeResp
            {
                IsValid = false,
                Error = "Disabled",
            };
        }

        var organization = await _connection.Filter<Entity, Organization>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Id, territory.EntityId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (organization == null)
        {
            _logger.LogInformation("{PostalCode} is serviced by {OrganizationId} that is disabled", postalCode, territory.EntityId);
            return new ValidatePostalCodeResp
            {
                IsValid = false,
                Error = "Organization Disabled",
            };
        }

        _logger.LogInformation("{PostalCode} is serviced by different {OrganizationId}: {LaunchCode}", postalCode, territory.EntityId, settingsForOtherOrg.ExternalId);
        return new ValidatePostalCodeResp
        {
            IsValid = true,
            OrganizationId = settingsForOtherOrg.ExternalId,
        };
    }

    [Authorize("scheduler")]
    [HttpPost("Session/PostalCode({postalCode})")]
    public async Task<SessionConfig> InitSessionByPostalCodeAsync([FromRoute] string postalCode, [FromQuery] string types)
    {
        using var scope = _logger.AddScope(new
        {
            Context.AccountId,
            PostalCode = postalCode,
            Types = types
        });

        _logger.LogInformation("Try to initiate Session by postal code");

        var list = string.IsNullOrWhiteSpace(types) ? [] : types.Split(",");

        postalCode = PI.Shared.Models.Lead.GetPostalCodeForLookup(postalCode);
        if (postalCode == null) throw new BadRequestException("Invalid postal code");

        var query = _connection.Filter<CustomObject>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.ObjectType, "ZeeTerritory")
                .Eq(x => x.ExternalId, postalCode)
                .Ne(x => x.IsActive, false)
            ;

        if (!list.IsEmpty())
        {
            if (list.Length == 1)
            {
                query.Eq(x => x.Properties["Type"], types);
            }
            else
            {
                query.In(x => x.Properties["Type"], list);
            }
        }

        var match = await query.FirstOrDefaultAsync();

        if (match == null)
        {
            _logger.LogInformation("Postalcode not found");
            throw new NotFoundException("Postal code not mapped");
        }

        var orgQuery = _connection.Filter<Entity>()
            .Eq(x => x.Id, match.EntityId)
            .Ne(x => x.IsActive, false);

        if (list.Contains("PPA"))
        {
            // TODO: 100% FCI, limit to ppa
            orgQuery.Eq("FCI.IsPPA", true);
        }

        var organization = await orgQuery.FirstOrDefaultAsync();
        if (organization == null)
        {
            _logger.LogInformation("{OrganizationId} not found or not active", match.EntityId);
            throw new NotFoundException("Entity is not available");
        }

        if (list.Contains("LMS"))
        {
            // check whether the postal code has been globally (for org, any lead type, any service) for the org:
            // - deactivated (LMS changes)
            // - or opted out (e.g. Tags="Zee: OptOut")
            // TODO: the Zee: OptOut is too FCI specific but... 
            var optedOut = await _connection.Filter<ServiceTracking>()
                .Eq(x => x.AccountId, organization.AccountId)
                .Eq(x => x.EntityId, organization.Id)
                .Eq(x => x.PostalCode, postalCode)
                .Eq(x => x.Name, null)
                .Eq(x => x.LeadTypeId, null)
                .OrBuilder(
                    q => q.Eq(x => x.IsActive, false),
                    q => q.AnyEq(x => x.Tags, "Zee: OptOut")
                )
                .FirstOrDefaultAsync();

            if (optedOut != null)
            {
                _logger.LogInformation("Postalcode for {OrganizationId} is {IsInactive} or {OptedOut}", organization.Id, !optedOut.IsActive, (optedOut.Tags?.Any(x => x == "Zee: OptOut") ?? false));
                throw new NotFoundException("Postal code not mapped");
            }
        }

        _logger.LogInformation("Found {EntityId}", match.EntityId);
        var session = await _scheduler.InitiateSessionForOrganizationAsync(Context, match.EntityId, Request.Headers.Referer.FirstOrDefault());

        return await MapAsync(session);
    }

    [Authorize("scheduler")]
    [HttpGet("Organization({orgId})")]
    [HttpPost("Session/Organization({orgId})")]
    public async Task<SessionConfig> InitSessionForOrganizationAsync([FromRoute] string orgId)
    {
        using var scope = _logger.AddScope(new
        {
            OrganizationId = orgId,
        });

        _logger.LogInformation("Initiate Session for Organization");

        var session = await _scheduler.InitiateSessionByLaunchCodeAsync(Context, orgId, Request.Headers.Referer.FirstOrDefault());

        return await MapAsync(session);
    }

    // // DO NOT REMOVE UNTIL ONLINEIMAGE REPLACES IT
    // [Obsolete("use /app/v1/...")]
    // [Authorize("scheduler")]
    // [HttpGet("/app/[controller]({sessionId})/Slots")]
    // public async Task<IEnumerable<TimeSlot>> OldGetSlotsAsync([FromRoute] Guid sessionId, DateTime? start, DateTime? end)
    // {
    //     using var scope = _logger.AddScope(new
    //     {
    //         SessionId = sessionId,
    //         Start = start,
    //         End = end,
    //     });
    //
    //     _logger.LogInformation("Get Slots (obsolete)");
    //
    //     return await _scheduler.GetSlotsAsync(Context, sessionId, start, end);
    // }

    [Authorize("scheduler")]
    [HttpGet("/app/v1/[controller]({sessionId})/Slots")]
    public async Task<SlotsResp> GetSlotsAsync([FromRoute] Guid sessionId, DateTime? start, DateTime? end, bool includeUnavailable = false)
    {
        using var scope = _logger.AddScope(new
        {
            SessionId = sessionId,
            Start = start,
            End = end,
        });

        _logger.LogInformation("Get Slots");

        var session = await _scheduler.GetExistingSession(Context, sessionId);
        var slots = includeUnavailable ? await _scheduler.GetUnavailableSlotsAsync(Context, session, start, end) : await _scheduler.GetSlotsAsync(Context, session, start, end);

        var result = new SlotsResp
        {
            SuggestedSlots = _scheduler.CalculateSuggestedSlots(session.TimeZoneId, slots), // not used in scheduler 
            Slots = slots,
            TimeZoneId = session.TimeZoneId,
        };

        return result;
    }

    [Authorize("scheduler")]
    [HttpGet("/app/[controller]({sessionId})/Lead")]
    [Consumes("application/json")]
    public async Task<Lead> GetLeadAsync(Guid sessionId)
    {
        using var scope = _logger.AddScope(new
        {
            SessionId = sessionId,
        });

        _logger.LogInformation("Get Lead");

        var session = await LoadSessionLeadAsync(sessionId);

        _logger.LogInformation("Got {LeadId} Info: {IsActive} {ConvertedOn}", session.LeadId, session.Lead?.IsActive, session.Lead?.ConvertedOn);

        return _mapper.Map<Lead>(session.Lead);
    }

    private async Task<SchedulerSession> LoadSessionLeadAsync(Guid sessionId)
    {
        var session = await _scheduler.GetExistingSession(Context, sessionId);
        if (!session.LeadId.HasValue)
        {
            _logger.LogError("Session doesn't have a Lead");
            throw new NotFoundException("No Lead");
        }

        if (session.Lead?.Id != session.LeadId)
        {
            session.Lead = await _connection.Filter<PI.Shared.Models.Lead>()
                .Eq(x => x.AccountId, Context.AccountId)
                .Eq(x => x.Id, session.LeadId)
                .FirstOrDefaultAsync();
        }

        return session;
    }

    [Authorize("scheduler")]
    [HttpPut("/app/[controller]({sessionId})/Lead")]
    public async Task<Lead> UpdateLeadAsync(Guid sessionId, [FromBody] UpdateLeadRequest request)
    {
        using var scope = _logger.AddScope(new
        {
            SessionId = sessionId,
        });

        _logger.LogInformation("Update Lead");

        var session = await LoadSessionLeadAsync(sessionId);
        var lead = session.Lead;
        if (lead == null) throw new ForbiddenException();

        _logger.LogInformation("Update Existing {LeadId}", lead.Id);

        var modifiedFields = new Dictionary<string, object>();
        var properties = new Dictionary<string, string>
        {
            { PI.Shared.Models.Lead.PropertyName_Address, request.Address },
            { PI.Shared.Models.Lead.PropertyName_City, request.City },
            { PI.Shared.Models.Lead.PropertyName_Country, request.Country },
            { PI.Shared.Models.Lead.PropertyName_Email, request.Email },
            { PI.Shared.Models.Lead.PropertyName_Name, request.Name },
            { PI.Shared.Models.Lead.PropertyName_Phone, request.Phone },
            { PI.Shared.Models.Lead.PropertyName_PostalCode, request.PostalCode },
            { PI.Shared.Models.Lead.PropertyName_State, request.State },
        };

        var update = _connection.Filter<PI.Shared.Models.Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, lead.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor);

        foreach (var prop in properties)
        {
            var value = prop.Value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new BadRequestException($"{prop.Key} is required");
            }

            if (lead.Properties.TryGetValue(prop.Key, out var current) && Equals(current, value)) continue;

            lead.Properties[prop.Key] = value;
            modifiedFields.Add(prop.Key, value);

            if (prop.Key == PI.Shared.Models.Lead.PropertyName_Name && PersonName.TryParse(value, out var parsed))
            {
                // update "calculated" properties
                lead.Name = value;

                lead.Properties[PI.Shared.Models.Lead.PropertyName_FirstName] = parsed.FirstName;
                modifiedFields.Add(PI.Shared.Models.Lead.PropertyName_FirstName, parsed.FirstName);

                lead.Properties[PI.Shared.Models.Lead.PropertyName_LastName] = parsed.LastName;
                modifiedFields.Add(PI.Shared.Models.Lead.PropertyName_LastName, parsed.LastName);
            }
        }

        // explicit properties
        if (!string.IsNullOrWhiteSpace(request.Notes) && !Equals(request.Notes, lead.Notes))
        {
            lead.Notes = request.Notes.Trim();
            modifiedFields.Add(nameof(Lead.Notes), lead.Notes);
        }

        if (modifiedFields.Count > 0)
        {
            // override Properties and all derived  
            MongoConnectionExtensions.ResetUsingProperties(update, lead);
            // update.SetProperties(lead);
        }

        if (request.SMSOptIn.HasValue)
        {
            var preference = request.SMSOptIn.Value ? CommunicationPreference.OptedIn : CommunicationPreference.OptedOut;
            if (lead.CommunicationPreferences == null || !lead.CommunicationPreferences.TryGetValue(CommunicationChannel.SMS, out var current) || !string.Equals(current, preference))
            {
                update.Set(x => x.CommunicationPreferences[CommunicationChannel.SMS], preference);
                modifiedFields.Add("CommunicationPreferences|SMS", preference);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.TrustedFormCertUrl))
        {
            // TODO: trustedFormCertUrl, lat, lng
            // ...
        }

        // TODO: other parameters ? => extra properties?
        // ...

        if (modifiedFields.Count > 0)
        {
            session.Lead = await update.UpdateAndGetOneAsync();

            await _objectTypeService.FireObjectUpdatedAsync(Context, session.Lead, modifiedFields, e => { e.Description = "Contact info updated"; });
        }

        return _mapper.Map<Lead>(session.Lead);
    }

    [Authorize("scheduler")]
    [HttpPost("/app/[controller]({sessionId})/Lead")]
    [HttpPost("/app/[controller]({sessionId})/Lead({leadTypeId})")]
    [Consumes("application/json")]
    public async Task<Lead> AddLeadAsync(Guid sessionId, [FromBody] ExpandoObject payload, Guid? leadTypeId)
    {
        using var scope = _logger.AddScope(new
        {
            SessionId = sessionId,
            LeadTypeId = leadTypeId,
        });

        _logger.LogInformation("Create Lead");

        var session = await _scheduler.GetExistingSession(Context, sessionId);
        if (session.LeadId.HasValue)
        {
            _logger.LogError("There is already a lead in this session");
            throw new BadRequestException("Can't change lead after is created");
        }

        await AddLeadAsync(session, payload, leadTypeId);

        _logger.LogInformation("{LeadId} Created", session.Lead?.Id);

        return _mapper.Map<Lead>(session.Lead);
    }

    [Authorize("schedulerExisting")]
    [HttpDelete("/app/[controller]({sessionId})/Appointment({id})")]
    public async Task<Appointment> DeleteAppointmentAsync([FromRoute] Guid sessionId, [FromRoute] Guid id)
    {
        var session = await _scheduler.GetExistingSession(Context, sessionId);

        var appt = await _connection.Filter<PI.Shared.Models.Appointment>()
            .Eq(x => x.LeadId, LeadId)
            .Eq(x => x.AccountId, session.AccountId)
            .Eq(x => x.Id, id)
            .FirstOrDefaultAsync();

        if (appt == null) throw NotFoundException.New<PI.Shared.Models.Appointment>(id);

        appt = await _scheduler.CancelAppointmentAndUpdateLeadAsync(Context, appt);

        return await ConvertAsync(appt);
    }

    private async Task<Appointment> ConvertAsync(PI.Shared.Models.Appointment appointment)
    {
        // omit
        appointment.Integrations = null;

        return appointment;
    }

    [Authorize("scheduler")]
    [HttpPost("/app/[controller]({sessionId})/Appointment")]
    [RequestSizeLimit(10_000)]
    public async Task<Appointment> AppointmentAsync([FromRoute] Guid sessionId, [FromBody] AppointmentReq appt)
    {
        var session = await _scheduler.GetExistingSession(Context, sessionId);
        var allowUnavailable = session.Settings.UnavailableSlots?.AssignEntityId.HasValue ?? false;

        using var scope = _logger.AddScope(new
        {
            SessionId = sessionId,
            appt.Start,
            appt.End,
            session.LeadId,
            session.Settings?.UnavailableSlots?.AssignEntityId,
            AllowUnavailable = allowUnavailable,
            session.Settings?.ClientId
        });

        _logger.LogInformation("Create Appointment");

        var nextAppt = await _connection.Filter<PI.Shared.Models.Appointment>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.LeadId, session.LeadId)
            .Eq(x => x.CancelledOn, null)
            .Gt(x => x.Start, DateTime.UtcNow)
            .SortAsc(x => x.Start)
            .FirstOrDefaultAsync();

        if (nextAppt != null)
        {
            _logger.LogInformation("There is already an active {AppointmentId}: {Start}", nextAppt.Id, nextAppt.Start);
        }

        DateTime start = appt.Start.Value;
        DateTime end = appt.End.Value;

        try
        {
            var integration = new AppointmentIntegration
            {
                IntegrationId = IntegrationIds.AutoScheduler,
                ExternalId = Guid.NewGuid().ToString(),
                Status = "Scheduled",
            };

            var launchQueryString = HttpContext.User?.Claims?.FirstOrDefault(x => x.Type == "client_query_string")?.Value;
            if (!string.IsNullOrWhiteSpace(launchQueryString) && launchQueryString.StartsWith("?"))
            {
                var query = launchQueryString[1..].Split('&');
                var value = query.Select(x => x.Split('=')).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
                integration.Data = value;
            }

            session.Appointment = await _scheduler.CreateAppointmentAsync(Context, session, start, end, integration, allowUnavailable);

            _logger.LogInformation("{AppointmentId} Created", session.Appointment?.Id);

            if (nextAppt != null)
            {
                _logger.LogInformation("Cancel previous Appointment");
                await _scheduler.CancelAppointmentAsync(Context, nextAppt.Id, integration.IntegrationId);
            }

            if (!string.IsNullOrWhiteSpace(appt.Content))
            {
                await AddNoteAsync(appt, session.Appointment);
            }

            return await ConvertAsync(session.Appointment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to book appointment for {sessionId}, {start}-{end}", sessionId, start, end);

            await _connection.Filter<SchedulerSession>()
                .Eq(x => x.Id, session.Id)
                .Update
                .Push(x => x.Errors, new SchedulingError
                {
                    Slot = new TimeSlot
                    {
                        Start = start,
                        End = end,
                    },
                    Error = ex.Message,
                })
                .UpdateOneAsync();

            throw;
        }
    }

    private async Task<Note> AddNoteAsync(NoteReq request, PI.Shared.Models.Appointment appointment)
    {
        var integrationId = IntegrationIds.AutoScheduler;
        var integrationName = IntegrationIds.GetName(integrationId);

        var note = new Note
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value, // 
            Name = request.Subject ?? "Appointment",
            // Description = // make it be the plain text representation of content
            Content = request.Content,
            // ContentFormat = request.ContentFormat,
            ContentType = request.ContentFormat switch
            {
                ContentFormat.Html => "text/html",
                ContentFormat.PlainText => "text/plain",
                ContentFormat.Markdown => "text/markdown",
                _ => null
            },
            Refs = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>($"{nameof(PI.Shared.Models.Lead)}Id", appointment.LeadId),
                new KeyValuePair<string, object>($"{nameof(PI.Shared.Models.Appointment)}Id", appointment.Id),
                new KeyValuePair<string, object>($"{nameof(Integration)}Id", integrationId),
            },
        };

        var objectType = await _objectTypeService.GetAsync(Context, note.GetType().Name);

        note.FlowId = objectType?.InitialFlowId;
        note.ObjectStatusId = objectType?.InitialObjectStatusId;

        note = await _connection.InsertAsync(note);

        await _objectTypeService.FireCreateEventAsync(Context, note, e =>
        {
            e.Description ??= note.Name;
            e.Action ??= "ObjectCreated";

            e.SetRefValue(nameof(PI.Shared.Models.Lead), appointment.LeadId);
            e.SetRefValue(nameof(PI.Shared.Models.Appointment), appointment.Id);
            e.SetMetaValue(nameof(PI.Shared.Models.Appointment), appointment.Name);

            e.SetRefValue(nameof(Integration), integrationId);
            e.SetMetaValue(nameof(Integration), integrationName);
            e.SetMetaValue(nameof(PI.Shared.Models.Appointment.LocalDate), appointment.LocalDate);
            e.SetMetaValue(nameof(PI.Shared.Models.Appointment.LocalTime), appointment.LocalTime);
        });

        return note;
    }

    private async Task<Note> AddNoteAsync(NoteReq request, PI.Shared.Models.Lead lead)
    {
        var integrationId = IntegrationIds.AutoScheduler;
        var integrationName = IntegrationIds.GetName(integrationId);

        var note = new Note
        {
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value, // 
            Name = request.Subject ?? "Lead",
            // Description = // make it be the plain text representation of content
            Content = request.Content,
            // ContentFormat = request.ContentFormat,
            ContentType = request.ContentFormat switch
            {
                ContentFormat.Html => "text/html",
                ContentFormat.PlainText => "text/plain",
                ContentFormat.Markdown => "text/markdown",
                _ => null
            },
            Refs = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>($"{nameof(PI.Shared.Models.Lead)}Id", lead.Id),
                new KeyValuePair<string, object>($"{nameof(Integration)}Id", integrationId),
            },
        };

        var objectType = await _objectTypeService.GetAsync(Context, note.GetType().Name);

        note.FlowId = objectType?.InitialFlowId;
        note.ObjectStatusId = objectType?.InitialObjectStatusId;

        note = await _connection.InsertAsync(note);

        await _objectTypeService.FireCreateEventAsync(Context, note, e =>
        {
            e.Description ??= note.Name;
            e.Action ??= "ObjectCreated";

            e.SetRefValue(nameof(PI.Shared.Models.Lead), lead.Id);
            e.SetMetaValue(nameof(PI.Shared.Models.Lead), lead.Name);

            e.SetRefValue(nameof(Integration), integrationId);
            e.SetMetaValue(nameof(Integration), integrationName);
        });

        return note;
    }

    private async Task<Guid> AddLeadAsync(SchedulerSession session, IDictionary<string, object> payload, Guid? leadTypeId)
    {
        var request = LeadBuilderService.BuildLeadRequestObject(HttpContext);
        payload["Request"] = request.Request;
        request.Body = JsonConvert.SerializeObject(payload);
        await _connection.InsertAsync(request);

        var entity = await _connection.Filter<Entity>()
            .Eq(x => x.AccountId, session.AccountId)
            .Eq(x => x.Id, session.EntityId)
            .FirstOrDefaultAsync();

        // fallback to default for the config
        leadTypeId ??= session.Settings.LeadTypeId;

        var leadType = await _connection.Filter<LeadType>()
            .Eq(x => x.AccountId, session.AccountId)
            .Eq(x => x.Id, leadTypeId.Value)
            .FirstOrDefaultAsync();

        // TODO: MOVE TO LMS (e.g. ObjectType Service instead of LeadBuilderService)
        // ...

        var builder = await _leadBuilderService.AddAsync(entity.Context, leadType, request.Body);
        if (builder.Failed)
        {
            throw new BadRequestException(builder.Error);
        }

        var lead = builder.Result;

        var modifiedFields = new Dictionary<string, object>();
        var updateQuery = _connection.Filter<PI.Shared.Models.Lead>()
            .Eq(x => x.Id, lead.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor);

        // hack to handle SMSOptIn (propagate from properties to communication preferences)
        if (lead.Properties.TryGetValue("SmsOptIn", out var smsOptIn) && smsOptIn is bool smsOption)
        {
            var preference = smsOption ? CommunicationPreference.OptedIn : CommunicationPreference.OptedOut;
            updateQuery.Set(x => x.CommunicationPreferences[CommunicationChannel.SMS], preference);
            modifiedFields.Add($"{nameof(Lead.CommunicationPreferences)}|{nameof(CommunicationChannel.SMS)}", preference);
        }

        if (payload.TryGetStrParam("trustedFormCertUrl", out var trustedFormCertUrl) && !string.IsNullOrWhiteSpace(trustedFormCertUrl))
        {
            updateQuery.Set(x => x.TrustedFormCert, trustedFormCertUrl);
            modifiedFields.Add(nameof(PI.Shared.Models.Lead.TrustedFormCert), trustedFormCertUrl);
        }

        if (payload.TryGetStrParam("lat", out var latitude) && !string.IsNullOrWhiteSpace(latitude) &&
            payload.TryGetStrParam("lng", out var longitude) && !string.IsNullOrWhiteSpace(longitude) &&
            decimal.TryParse(latitude, out var lat) && decimal.TryParse(longitude, out var lng))
        {
            updateQuery.Set(x => x.Location, new Point
            {
                Coordinates = new[]
                {
                    lng,
                    lat
                }
            });
            modifiedFields.Add(nameof(PI.Shared.Models.Lead.Location), $"{longitude},{latitude}");
        }

        if (payload.TryGetValue("extraProperties", out var extraPropsObj) && extraPropsObj is IDictionary<string, object> extraProperties && extraProperties.Count > 0)
        {
            updateQuery.Set("ExtraProperties", extraProperties);
            modifiedFields.Add("ExtraProperties", string.Join(", ", extraProperties.Keys));
        }

        if (modifiedFields.Count > 0)
        {
            lead = await updateQuery.UpdateAndGetOneAsync();

            await _objectTypeService.FireObjectUpdatedAsync(
                Context,
                lead,
                modifiedFields
            );
        }

        await _connection.Filter<SchedulerSession>()
            .Eq(x => x.Id, session.Id)
            .Update
            .Set(x => x.LeadId, lead.Id)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, Context.Actor)
            .UpdateAndGetOneAsync();

        session.Lead = lead;

        return builder.LeadId;
    }

    private async Task<SessionConfig> MapAsync(SchedulerSession session)
    {
        var result = new SessionConfig
        {
            Id = session.Id,
            EntityId = session.EntityId,
            Name = session.Name,
            TimeZoneId = session.TimeZoneId,
            PrimaryColorRgba = "1c294a",
            LogoUrl = "https://links.fci.cloud/logo.png",
            AllowUnavailableSlots = session.Settings?.UnavailableSlots?.AssignEntityId.HasValue ?? false,
        };

        var tasks = getTasks().ToArray();

        result.Pages = new Dictionary<SchedulerPage, SchedulerPageSettings>
        {
            {
                SchedulerPage.contactInfo, new SchedulerPageSettings
                {
                    Header = "Your Contact Info",
                    SubHeader = "Let us know who you are",
                }
            },
            {
                SchedulerPage.scheduler, new SchedulerPageSettings
                {
                    Settings = new Dictionary<string, SchedulerFeatureSetting>
                    {
                        {
                            SchedulerFeatureSetting.SuggestedSlotsSection,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = false, // hide quick picks!
                                Label = "Quick Picks",
                            }
                        },
                        {
                            SchedulerFeatureSetting.DateTimePickerSection,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                Label = "Pick a date & time to book your appointment",
                            }
                        },
                    },
                }
            },
            {
                SchedulerPage.appointmentConfirmation, new SchedulerPageSettings
                {
                    Header = "Your Free In-Home Consultation",
                    SubHeader = "Review and Confirm",
                    Settings = new Dictionary<string, SchedulerFeatureSetting>
                    {
                        {
                            SchedulerFeatureSetting.RescheduleExistingAppointment,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                IsAllowed = true,
                                Label = "reschedule",
                            }
                        },
                        {
                            SchedulerFeatureSetting.ChangeAppointment,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                IsAllowed = true,
                                Label = "change",
                            }
                        },
                        {
                            SchedulerFeatureSetting.ScheduleAppointment,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                IsAllowed = true,
                                Label = "Schedule",
                            }
                        },
                        {
                            SchedulerFeatureSetting.CancelExistingAppointment,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                FallbackInstructions = "Please contact us to cancel your Free In-Home Consultation.",
                                Label = "Cancel",
                            }
                        },
                        {
                            SchedulerFeatureSetting.ContactInfoSection,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                Label = "Your Information",
                            }
                        },
                        {
                            SchedulerFeatureSetting.AppointmentSection,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                Label = "Schedule",
                            }
                        }
                    }
                }
            },
            {
                SchedulerPage.appointmentScheduled, new SchedulerPageSettings
                {
                    Header = "Congratulations! You’re Scheduled, We Can’t Wait to Meet You.",
                    Settings = new Dictionary<string, SchedulerFeatureSetting>
                    {
                        {
                            SchedulerFeatureSetting.AppointmentSection,
                            new SchedulerFeatureSetting
                            {
                                IsVisible = true,
                                IsAllowed = true,
                                FallbackInstructions = "Almost Done. Tell us about your project...",
                                Label = "Add Note",
                            }
                        },
                    }
                }
            }
        };

        await Task.WhenAll(tasks);

        return result;

        IEnumerable<Task> getTasks()
        {
            if (session.LeadId.HasValue) yield return loadLeadAsync();
            if (session.AppointmentId.HasValue) yield return loadAppointmentAsync();
            yield return loadOrganizationAsync();
        }

        async Task loadOrganizationAsync()
        {
            result.Organization = await GetOrganizationAsync(session);
        }

        async Task loadAppointmentAsync()
        {
            if (session.Appointment?.Id != session.AppointmentId)
            {
                session.Appointment = await _connection.Filter<PI.Shared.Models.Appointment>()
                    .Eq(x => x.Id, session.AppointmentId.Value)
                    .FirstOrDefaultAsync();
            }

            if (session.Appointment.IsActive && session.Appointment.Start > DateTime.UtcNow)
            {
                // only return appt if it is active
                result.Appointment = _mapper.Map<SparseAppointment>(session.Appointment);
            }
        }

        async Task loadLeadAsync()
        {
            if (session.Lead?.Id != session.LeadId)
            {
                session.Lead = await _connection.Filter<PI.Shared.Models.Lead>()
                    .Eq(x => x.Id, session.LeadId.Value)
                    .FirstOrDefaultAsync();
            }

            result.Lead = _mapper.Map<SparseLead>(session.Lead);
        }
    }

    protected async Task<OrganizationResp> GetOrganizationAsync(SchedulerSession session)
    {
        if (session.Entity?.Id != session.EntityId)
        {
            session.Entity = await _connection.Filter<Entity, Organization>()
                .Eq(x => x.Id, session.EntityId)
                .FirstOrDefaultAsync();
        }

        if (session.Entity == null) throw NotFoundException.New<Organization>(session.EntityId);

        // hack for now
        // TODO: get from the schedulersettings? 
        // ...
        var sf = session.Entity.Identities?.FirstOrDefault(x => x.IdentityProviderId == nameof(ExternalProvider.Salesforce));

        var result = new OrganizationResp
        {
            Id = session.Entity.Id,
            Name = sf.GetSalesforceFieldValue("Branch_Territory_Name__c", session.Entity.Name),
            // Description = sf.GetSalesforceFieldValue("INET_WhoIsFCI__c", session.Entity.Description),
            Address = sf.GetSalesforceFieldValue("Street", default(string)),
            City = sf.GetSalesforceFieldValue("City", default(string)),
            State = sf.GetSalesforceFieldValue("State", default(string)),
            PostalCode = sf.GetSalesforceFieldValue("PostalCode", default(string)),
            Country = sf.GetSalesforceFieldValue("Country", default(string)),
            Phone = sf.GetSalesforceFieldValue("Branch_Phone_Number__c", default(string)),
            Email = session.Entity.Email,
            TimeZoneId = session.Entity.TimeZoneId,
            // ...
        };

        return result;
    }

    public class NoteReq
    {
        public ContentFormat ContentFormat { get; set; } = ContentFormat.PlainText;
        public string Content { get; set; }
        public string Subject { get; set; }
    }

    public class AppointmentReq : NoteReq
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    public class ValidatePostalCodeResp
    {
        /// <summary>
        /// is valid for the current org
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Organization Id, if valid.
        /// </summary>
        public string OrganizationId { get; set; }

        /// <summary>
        /// Why it is not valid
        /// </summary>
        public string Error { get; set; }
    }

    public class OrganizationResp
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PostalCode { get; set; }
        public string State { get; set; }
        public string WebsiteUrl { get; set; }
        public string TimeZoneId { get; set; }
    }

    public class SparseAppointment
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string LocalDate { get; set; }
        public string LocalTime { get; set; }
        public string TimeZoneId { get; set; }
    }

    public class UpdateLeadRequest
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PostalCode { get; set; }
        public string State { get; set; }
        public string Notes { get; set; }

        public bool? SMSOptIn { get; set; }

        public string TrustedFormCertUrl { get; set; }

        [JsonProperty("lat")] public string Latitude { get; set; }

        [JsonProperty("lng")] public string Longitude { get; set; }
    }

    public class SparseLead : UpdateLeadRequest
    {
        public Guid Id { get; set; }
        public Dictionary<string, string> CommunicationPreferences { get; set; }
        public string TimeZoneId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class SchedulerFeatureSetting
    {
        public const string ChangeAppointment = "changeAppointment";
        public const string RescheduleExistingAppointment = "rescheduleExistingAppointment";
        public const string ScheduleAppointment = "scheduleAppointment";
        public const string CancelExistingAppointment = "cancelExistingAppointment";
        public const string ContactInfoSection = "contactInfoSection";
        public const string AppointmentSection = "appointmentSection";
        public const string SuggestedSlotsSection = "suggestedSlotsSection";
        public const string DateTimePickerSection = "dateTimePickerSection";

        public string Label { get; set; }
        public bool IsAllowed { get; set; }
        public bool IsVisible { get; set; }
        public string FallbackInstructions { get; set; }
    }

    public enum SchedulerPage
    {
        contactInfo,
        scheduler,
        appointmentConfirmation,
        appointmentScheduled,
    }

    public class SchedulerPageSettings
    {
        public string Header { get; set; }
        public string SubHeader { get; set; }
        public Dictionary<string, SchedulerFeatureSetting> Settings { get; set; }
    }

    public class SessionConfig
    {
        public Guid Id { get; set; }
        public Guid EntityId { get; set; }
        public string Name { get; set; }
        public string TimeZoneId { get; set; }

        // extended
        public OrganizationResp Organization { get; set; }
        public SparseAppointment Appointment { get; set; }
        public SparseLead Lead { get; set; }
        public Dictionary<SchedulerPage, SchedulerPageSettings> Pages { get; set; }
        public string PrimaryColorRgba { get; set; }
        public string LogoUrl { get; set; }

        public bool AllowUnavailableSlots { get; set; }
    }

    public class SparseLeadProfile : Profile
    {
        public SparseLeadProfile()
        {
            CreateMap<PI.Shared.Models.Lead, SparseLead>(MemberList.Destination)
                .ForMember(d => d.SMSOptIn, o => o.MapFrom(s => (s.CommunicationPreferences != null && s.CommunicationPreferences.ContainsKey(CommunicationChannel.SMS)) ? s.CommunicationPreferences[CommunicationChannel.SMS] == CommunicationPreference.OptedIn : default(bool?)
                ))
                .ForMember(d => d.TrustedFormCertUrl, o => o.MapFrom(s => s.TrustedFormCert))
                .ForMember(x => x.Latitude, o => o.Ignore())
                .ForMember(x => x.Longitude, o => o.Ignore())
                ;
        }
    }

    public class SparseAppointmentProfile : Profile
    {
        public SparseAppointmentProfile()
        {
            CreateMap<PI.Shared.Models.Appointment, SparseAppointment>(MemberList.Destination);
        }
    }
}