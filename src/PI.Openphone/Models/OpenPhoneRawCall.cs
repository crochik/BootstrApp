using System;

namespace PI.Openphone.Models;

public class OpenPhoneRawCall : AbstractOpenPhoneCommObject
{
    // "voicemail" : null,
    public OpenPhoneMedia Voicemail { get; set; }
    
    // "answeredAt" : null,
    public DateTime? AnsweredAt { get; set; }
    
    // "answeredBy" : null,
    public string AnsweredBy { get; set; }
    
    // "completedAt" : ISODate("2024-07-23T11:44:41.000-04:00"),
    public DateTime? CompletedAt { get; set; }
}