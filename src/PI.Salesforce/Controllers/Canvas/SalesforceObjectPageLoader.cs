using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Salesforce.Models.Canvas;
using PI.Shared.Services;
using Services;
using User = PI.Shared.Models.User;

namespace Controllers;

public abstract class SalesforceObjectPageLoader<T> : AbstractPageLoader
    where T : IObjectChangeProcessor
{
    private readonly T _processor;

    protected SalesforceObjectPageLoader(ILogger<SalesforceObjectPageLoader<T>> logger, MongoConnection connection, AuthorizationService authorizationService, T processor)
        : base(logger, connection, authorizationService)
    {
        _processor = processor;
    }

    public override async Task<Result<string>> LoadAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height)
    {
        var error = await InitAsync(signedRequest, user, client, record, page, height);
        if (error != null) return Result<string>.Error(error);

        error = await LoadObjectAsync();
        if (error != null) return Result<string>.Error(error);

        return GetRedirection();
    }

    private async Task<string> LoadObjectAsync()
    {
        var (sf, obj) = await _processor.ProcessChangeAsync(User.AccountId, Record.Id, null);

        var lead = obj as Lead;
        switch (obj)
        {
            case SfWorkOrderObject workOrder:
                lead = await _connection.Filter<Lead>()
                    .Eq(x => x.AccountId, User.AccountId)
                    .Eq(x => x.Id, workOrder.LeadId)
                    .FirstOrDefaultAsync();
                break;
        }

        if (lead != null && !lead.IsActive) return "Lead is no longer active.";

        var page = default(string);
        if (string.IsNullOrWhiteSpace(Page) || Page == "Scheduler" || Page == "EmbeddedScheduler")
        {
            // default page ?
            page = obj switch
            {
                Lead => $"page://EmbeddedScheduler?OrganizationId={lead.EntityId}&LeadId={lead.Id}",
                SfWorkOrderObject workOrder => $"page://EmbeddedScheduler?OrganizationId={lead.EntityId}&LeadId={lead.Id}&Refs|sf_WorkOrder={workOrder.ExternalId}", // &sf_WorkOrder={workOrder.ExternalId}
                _ => null,
            };
        }
        else
        {
            var context = User.Context;
            var appLink = await _connection.GetProfileElementAsync<AppPageLink>(
                context,
                q => q
                    .Eq(x => x.ObjectType, _processor.ObjectType)
                    .Eq(x => x.Name, Page)
                    .Ne(x => x.IsActive, false)
            );

            if (appLink != null)
            {
                // TODO: actually load the object using the service
                // can't use JsonObjectConverter.Convert<ExpandoObject>(obj) because it will not respect [BsonId]
                // ...
                var limitedObjectContext = new Dictionary<string, object>
                {
                    {
                        "Object", new Dictionary<string, object>
                        {
                            { "_id", obj.Id }
                        }
                    }
                };

                if (ExpressionEvaluatorService.TryResolve(context, limitedObjectContext, appLink.Url, out var resolvedUrl) && resolvedUrl is string pageUrl)
                {
                    page = pageUrl;
                }
            }
            else
            {
                page = $"page:/{Page}?id={obj.Id}";
            }
        }

        if (page != null)
        {
            page = Convert.ToBase64String(Encoding.UTF8.GetBytes(page));

            Url += $"?PI{page}";
        }

        if (User.OrganizationId.HasValue && lead != null && User.OrganizationId.Value != lead?.EntityId)
        {
            _logger.LogInformation("User {OrganizationId} doesn't match Lead's: {EntityId}", User.OrganizationId, lead.EntityId);

            // try to find ghost user 
            var ghost = await _connection.Filter<Entity, PI.Shared.Models.User>()
                .Eq(x => x.AccountId, User.AccountId)
                .Eq(x => x.OrganizationId, lead.EntityId)
                .ElemMatchBuilder(
                    f => f.Identities,
                    q => q
                        .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Bootstrapp))
                        .Eq(x => x.ExternalId, User.Id.ToString())
                )
                .FirstOrDefaultAsync();

            if (ghost == null || !ghost.IsActive) return "User access to the Lead is forbidden.";

            _logger.LogInformation("Found {GhostUserId} for {UserId} @ {OrganizationId} to access {LeadId}", ghost.Id, User.Id, lead.EntityId, lead.Id);

            // recalculate claims with ghost user
            Claims = await _authorizationService.GetAllClaimsAsync(ghost, ProfileId.Value, Client);
        }

        return null;
    }
}

public class LeadPageLoader : SalesforceObjectPageLoader<IOnLeadChangeProcessor>
{
    public LeadPageLoader(ILogger<LeadPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService, IOnLeadChangeProcessor processor) : base(logger, connection, authorizationService, processor)
    {
    }
}

public class AccountPageLoader : SalesforceObjectPageLoader<IOnAccountChangeProcessor>
{
    public AccountPageLoader(ILogger<AccountPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService, IOnAccountChangeProcessor processor) : base(logger, connection, authorizationService, processor)
    {
    }
}

public class ServiceAppointmentPageLoader : SalesforceObjectPageLoader<IOnServiceAppointmentChangeProcessor>
{
    public ServiceAppointmentPageLoader(ILogger<ServiceAppointmentPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService, IOnServiceAppointmentChangeProcessor processor) : base(logger, connection, authorizationService, processor)
    {
    }
}

public class WorkOrderPageLoader : SalesforceObjectPageLoader<IOnWorkOrderChangeProcessor>
{
    public WorkOrderPageLoader(ILogger<WorkOrderPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService, IOnWorkOrderChangeProcessor processor) : base(logger, connection, authorizationService, processor)
    {
    }
}