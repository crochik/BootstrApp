using System;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Services;

namespace Services;

/*
    "Lead" : {
        "_t" : "LeadStreamConfig",
        "Name" : "Lead",
        "IntegrationId" : "a2a0b3d8-ae75-47a9-8f10-0bb20af9802f",
        "LeadTypeId" : "adf59834-9cca-47cb-bdbf-53be06b76b99",
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
public class LeadObjectImporter : AbstractLeadObjectImporter
{
    public override Guid LeadTypeId => Guid.Parse("adf59834-9cca-47cb-bdbf-53be06b76b99");
    public override string SourceObjectTypeName => "sf_Lead";

    public LeadObjectImporter(ILogger<LeadObjectImporter> logger, MongoConnection connection, ObjectTypeService objectTypeService, LeadBuilderService leadBuilderService) :
        base(logger, connection, objectTypeService, leadBuilderService)
    {
    }
}