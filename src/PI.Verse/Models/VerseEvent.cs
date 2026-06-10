using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Models;

public class VerseEvent
{
    /// <summary>
    /// This is the "Source" of the lead which can be used to track effecicay of specic advertising campaigns.
    /// </summary>
    public string ChannelWebsite { get; set; }
    public string City { get; set; }
    public string Email { get; set; }

    /// <summary>
    /// This is YOUR lead id which will be correspond to the "externalLeadId" when Verse sends data back regarding your lead.
    /// </summary>
    public Guid ExternalLeadID { get; set; } // string, but....
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string PostalCode { get; set; }
    public string State { get; set; }
    public string Street { get; set; }

    public string Phone { get; set; }
    public string PhoneCarrierType { get; set; }


    /// <summary>
    /// This is the Verse ID of the lead
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// this is the the comments from our Verse Concierge Team regarding the lead and its details
    /// </summary>
    public string LeadComment { get; set; }

    /// <summary>
    /// This is the Type of Lead: For Real Estate: "buyer" or "seller. 
    /// For Mortgage: "mortgage" for purchase or "refi" for refinance All other industries can pass any value.
    /// </summary>
    public string LeadType { get; set; }

    /// <summary>
    /// This is a direct URL to the lead's details in your Verse account.
    /// </summary>
    public string Link { get; set; }

    /// <summary>
    /// This is the details of the activity that occurred - can include the content of the inbound / outbound SMS message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Date and time the event occurred in yyyy-mm-dd hh:mm:ss in military time. This time is in UTC	
    /// </summary>
    public DateTime? EventDate { get; set; }

    /// <summary>
    /// This is the last method of communication used to engage with the lead, can be "sms", "call", "email".	
    /// </summary>
    public string LastCommunicationChannel { get; set; }

    /// <summary>
    /// only is applied to lead_unqualify and lead_activity when the lead is unqualified'
    /// </summary>
    public string ReasonUnqualified { get; set; }

    /// <summary>
    /// This field contains all of the custom questions and their responses in your script.
    /// </summary>
    public Dictionary<string, string> CustomQuestions { get; set; }

    /// <summary>
    /// This field contains the custom field values that were sent over with this lead.
    /// </summary>
    public Dictionary<string, string> CustomFields { get; set; }

    /// <summary>
    /// This is only sent if there is a dynamic owner
    /// </summary>
    public Dictionary<string, string> Owner { get; set; }

    /// <summary>
    /// This field is the name of the activity that triggered the webhook.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// This field is the name of the webhook you subscribed to.
    /// </summary>
    public string Event { get; set; }

    /// <summary>
    /// This is the Verse User ID that the lead was assigned to.
    /// NOTE: The Field "userId" is only included if you're subscribing to the ENTIRE TEAM, and not an individual account.	
    /// </summary>
    [JsonProperty("userID")]
    public string UserId { get; set; }
}