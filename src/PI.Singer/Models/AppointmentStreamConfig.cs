using System;
using PI.Shared.Models;

namespace Models
{
    public class AppointmentStreamConfig : SingerStreamConfig
    {
        public Guid IntegrationId { get; set; }
        public Guid LeadTypeId { get; set; }
        public ExternalProvider ExternalProvider { get; set; }

        // createdBy / ownerId => ????
        // status "open" ???
        // isDeleted ??? 

        public string ExternalIdField { get; set; } = "id";
        public string LeadExternalIdField { get; set; } = "accountId"; // AccountId (a.k.a. Customer) => LeadId
        public string CreatedByExternalIdField { get; set; } = "createdById";
        public string[] EntityExternalIdFields { get; set; } = new[] {
            "designAssociateC", // Design_Associate__c => needs lookup to find User 
            "serviceTerritoryId"
        };

        public string UrlField { get; set; } = "dynamicSessionLinkC"; // dynamicSessionLinkC  => url

        /// <summary>
        /// will build integration url by concatenating the externalid to it
        /// </summary>
        public string UrlPrefix { get; set; } = "https://fcifloors.my.salesforce.com/";

        public string StartField { get; set; } = "schedStartTime";
        public string EndField { get; set; } = "schedEndTime";
        public string CreatedOnField { get; set; }
        public string LastModifiedOnField { get; set; } = "lastModifiedDate";
        public Guid AppointmentTypeId { get; set; }

        public AppointmentStreamConfig()
        {
        }

        public AppointmentStreamConfig(Guid id)
        {
            AppointmentTypeId = id;
        }
    }
}