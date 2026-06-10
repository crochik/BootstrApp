using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;
/*
    "ServiceResource" : {
        "_t" : "UserStreamConfig",
        "Name" : "ServiceResource",
        "ExternalProvider" : "Salesforce",
        "ExternalIdField" : "relatedRecordId",
        "UpdateIdentityField" : "id",
        "UserNameField" : "name",
        "AdditionalExternalIdFields" : {
            "id" : "ServiceResource"
        },
        "InactiveConditions" : [ 
            {
                "FieldName" : "isDeleted",
                "Value" : true
            }, 
            {
                "FieldName" : "isActive",
                "Value" : false
            }
        ]
    },
*/
public class ServiceResourceUserObjectImporter : AbastractEntityObjectImporter<User>
{
    public override string SourceObjectTypeName => "sf_ServiceResource";
    public override bool IsMainIdentity => false;
    public override string IdentityTag => "ServiceResource";
    public override string CollectionName => "Entity";

    public ServiceResourceUserObjectImporter(ILogger<ServiceResourceUserObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }

    protected override async Task<User> GetAsync(IEntityContext context, SalesforceCustomObject row)
    {
        var existing = await base.GetAsync(context, row);
        if (existing == null && row.Properties.TryGetValue("RelatedRecordId", out var relatedRecordId))
        {
            // try to find using related 
            existing = await _connection.Filter<Entity, User>(CollectionName)
                .Eq(x => x.AccountId, context.AccountId.Value)
                .ElemMatchBuilder(
                    x => x.Identities,
                    q => q
                        .Eq(x => x.IdentityProviderId, nameof(ExternalProvider.Salesforce))
                        .Eq(x => x.ExternalId, relatedRecordId)
                )
                .FirstOrDefaultAsync();
        }

        return existing;
    }
}