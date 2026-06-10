using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

/*
    "ServiceTerritory" : {
        "_t" : "OrganizationStreamConfig",
        "Name" : "ServiceTerritory",
        "ExternalProvider" : "Salesforce",
        "ExternalIdField" : "id",
        "NameField" : "name",
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
public class ServiceTerritoryObjectImporter : AbastractEntityObjectImporter<Organization>
{
    public override bool IsMainIdentity => true;

    public override string IdentityTag => null;

    public override string SourceObjectTypeName => "sf_ServiceTerritory";
    public override string CollectionName => "Entity";

    public ServiceTerritoryObjectImporter(ILogger<ServiceTerritoryObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService) : 
        base(logger, connection, objectTypeService)
    {
    }
}