using System;

namespace PI.Openphone.Models;

public abstract class AbstractOpenPhoneCommObject
{
    // "id" : "AC...",
    public string Id { get; set; }
    
    // "object" : "call",
    public string Object { get; set; } // call
    
    // "from" : "+19843335756",
    public string From { get; set; }
    
    // "to" : "+19196676638",
    public string To { get; set; }
    
    // "direction" : "outgoing",
    public string Direction { get; set; } // outgoing, incoming
    
    // "media" : [ 
    // ],
    public OpenPhoneMedia[] Media { get; set; }

    // "status" : "completed",
    public string Status { get; set; }
    
    // "createdAt" : ISODate("2024-07-23T11:44:40.010-04:00"),
    public DateTime? CreatedAt { get; set; }

    // "createdBy" : null,
    public string CreatedBy { get; set; }
    
    // "userId" : "USc1agodfM",
    public string UserId { get; set; }
    
    // "phoneNumberId" : "PNLnD6VsE1",
    public string PhoneNumberId { get; set; }
    
    // "conversationId" : "CN..."   
    public string ConversationId { get; set; }
}