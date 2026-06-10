using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

/*
    "User" : {
        "_t" : "UserStreamConfig",
        "Name" : "User",
        "ExternalProvider" : "Salesforce",
        "ExternalIdField" : "id",
        "UpdateIdentityField" : "id",
        "UserNameField" : "name",
        "AdditionalExternalIdFields" : {
            "designAssociatesIdC" : "ServiceResource"
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
public class UserObjectImporter : AbastractEntityObjectImporter<User>
{
    public override string SourceObjectTypeName => "sf_User";

    public override bool IsMainIdentity => true;

    public override string IdentityTag => null;

    public override string CollectionName => "Entity";

    public UserObjectImporter(ILogger<UserObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService) :
        base(logger, connection, objectTypeService)
    {
    }
}
