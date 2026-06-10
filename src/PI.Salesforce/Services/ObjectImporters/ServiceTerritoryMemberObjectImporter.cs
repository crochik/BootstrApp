using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

/*
    "ServiceTerritoryMember" : {
        "_t" : "OrganizationMembershipStreamConfig",
        "Name" : "ServiceTerritoryMember",
        "ExternalProvider" : "Salesforce",
        "UserExternalIdField" : "serviceResourceId",
        "OrganiztionExternalIdField" : "serviceTerritoryId"
    },
*/
public class ServiceTerritoryMemberObjectImporter : AbstractObjectImporter<User>
{
    public ServiceTerritoryMemberObjectImporter(ILogger<ServiceTerritoryMemberObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }

    public override string SourceObjectTypeName => "sf_ServiceTerritoryMember";

    public override string CollectionName => "Entity";

    protected override Task<User> GetAsync(IEntityContext context, SalesforceCustomObject row)
    {
        var userId = GetRequired<string>(row, "ServiceResourceId");

        return _connection.Filter<Entity, User>(CollectionName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, userId)
            )
            .FirstOrDefaultAsync();
    }

    protected override ValueTask<WriteModel<User>> AddAsync(IEntityContext entityContext, SalesforceCustomObject src)
    {
        // do not add user without any other information?
        // ...
        return ValueTask.FromResult<WriteModel<User>>(null);
    }


    protected override async ValueTask<WriteModel<User>> UpdateAsync(IEntityContext context, SalesforceCustomObject src, User dst)
    {
        var organizationId = GetRequired<string>(src, "ServiceTerritoryId");

        var organization = await _connection.Filter<Entity, Organization>(CollectionName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .ElemMatchBuilder(
                x => x.Identities,
                q => q
                    .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                    .Eq(x => x.ExternalId, organizationId)
            )
            .FirstOrDefaultAsync();

        if (organization == null) return null;

        return _connection.Filter<Entity, User>(CollectionName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, dst.Id)
            .Update
                .Set(x => x.OrganizationId, organization.Id)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, context.Actor())
            .UpdateOneModel();
    }
}