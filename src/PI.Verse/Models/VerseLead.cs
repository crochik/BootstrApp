using System;
using System.Collections.Generic;
using AutoMapper;
using Newtonsoft.Json;

namespace Models;

public class VerseLead
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string City { get; set; }
    public string Email { get; set; }

    /// <summary>
    /// This field is to provide additional notes to the Concierge team regarding the lead.
    /// </summary>
    public string AdditionalNote { get; set; }
    
    [JsonProperty("phoneNumber")]
    public string Phone { get; set; }

    public string PostalCode { get; set; }

    [JsonProperty("street")]
    public string Address { get; set; }

    public string State { get; set; }

    /// <summary>
    /// This is the Type of Lead: For Real Estate: "buyer" or "seller. 
    /// For Mortgage: "mortgage" for purchase or "refi" for refinance All other industries can pass any value.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// This is YOUR lead id which will correspond to the "externalLeadId" when Verse sends data back regarding your lead.
    /// </summary>
    [JsonProperty("zapierLeadId")]
    public Guid Id { get; set; }

    /// <summary>
    /// This field will override the default appointment booking link used when booking an appointment for the lead.
    /// </summary>
    [JsonProperty("agent.calendly")]
    public string SchedulerUrl { get; set; }

    /// <summary>
    /// This field will override the default email address that is notified when the Lead is qualified or unqualified.
    /// </summary>
    [JsonProperty("agent.email")]
    public string AgentEmail { get; set; }

    /// <summary>
    /// This field will override the default first name that is used in Verse's campaigns for the Lead.
    /// </summary>
    [JsonProperty("agent.firstName")]
    public string AgentFirstName { get; set; }

    /// <summary>
    /// This field will override the default last name that is used in Verse's campaigns for the Lead.
    /// </summary>
    [JsonProperty("agent.lastName")]
    public string AgentLastName { get; set; }

    /// <summary>
    /// This field will override the default phone number that is used in live transferring a Lead.
    /// </summary>
    [JsonProperty("agent.phone")]
    public string AgentPhone { get; set; }

    /// <summary>
    /// This field will override the default Team name that is used in Verse's campaigns.
    /// </summary>
    [JsonProperty("agent.teamName")]
    public string AgentTeam { get; set; }

    /// <summary>
    /// This field will override the default CallConnect number that a lead is connected to for Callconnect accounts for this lead only.
    /// </summary>
    [JsonProperty("agent.callCenterRoutingNumber")]
    public string CallCenterRoutingNumber { get; set; }

    public string ChannelWebsite { get; set; }

    /// <summary>
    /// This field is used to create a single or multiple custom fields that can be used in Verse's Campaigns.
    /// </summary>
    public Dictionary<string, string> CustomFields { get; set; }
}

public class VerseLeadProfile : Profile
{
    public VerseLeadProfile()
    {
        CreateMap<PI.Shared.Models.Lead, Models.VerseLead>(MemberList.Destination)
            .ForMember(x => x.AdditionalNote, o => o.Ignore())
            .ForMember(x => x.AgentEmail, o => o.Ignore())
            .ForMember(x => x.AgentFirstName, o => o.Ignore())
            .ForMember(x => x.AgentLastName, o => o.Ignore())
            .ForMember(x => x.AgentPhone, o => o.Ignore())
            .ForMember(x => x.AgentTeam, o => o.Ignore())
            .ForMember(x => x.CallCenterRoutingNumber, o => o.Ignore())
            .ForMember(x => x.ChannelWebsite, o => o.Ignore())
            .ForMember(x => x.CustomFields, o => o.Ignore())
            .ForMember(x => x.SchedulerUrl, o => o.Ignore())
            .ForMember(x => x.Type, o => o.Ignore())
            ;
    }
}
