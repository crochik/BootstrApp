using System;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Services;

namespace Services;

/*
    "Account" : {
        "_t" : "LeadStreamConfig",
        "Name" : "Account",
        "IntegrationId" : "a2a0b3d8-ae75-47a9-8f10-0bb20af9802f",
        "LeadTypeId" : "c691fc87-2768-406d-808a-dc7a2f4e05e0",
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
public class AccountObjectImporter : AbstractLeadObjectImporter
{
    public override Guid LeadTypeId => Guid.Parse("c691fc87-2768-406d-808a-dc7a2f4e05e0");
    public override string SourceObjectTypeName => "sf_Account";

    public AccountObjectImporter(ILogger<AccountObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService, LeadBuilderService leadBuilderService) :
        base(logger, connection, objectTypeService, leadBuilderService)
    {
    }
}