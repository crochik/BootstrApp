using System;

namespace PI.Openphone.Models;

public class OpenPhoneRawEvent
{
    public const string ContactUpdated = "contact.updated";
    public const string ContactDeleted = "contact.deleted";
    public const string CallRecordingCompleted = "call.recording.completed";
    public const string CallCompleted = "call.completed";
    public const string CallRinging = "call.ringing";
    public const string MessageDelivered = "message.delivered";
    public const string MessageReceived = "message.received";

    public string Id { get; set; }
    public string Object { get; set; }
    public string ApiVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Type { get; set; }
    public OpenPhoneEventData Data { get; set; }

    public string Name => GetName();
    public string Description => GetDescription();

    public string PhoneNumber => Type switch
    {
        MessageReceived => Data.From,
        MessageDelivered => Data.To,
        CallRinging => Data.Direction == "Incoming" ? Data.From : Data.To,
        CallCompleted => Data.Direction == "Incoming" ? Data.From : Data.To,
        CallRecordingCompleted => Data.Direction == "Incoming" ? Data.From : Data.To,
        ContactUpdated => null,
        ContactDeleted => null,
        _ => "Unknown"
    };

    private string GetDescription()
    {
        return Type switch
        {
            MessageReceived => Data.Body,
            MessageDelivered => Data.Body,
            CallRinging => null,
            CallCompleted => null,
            CallRecordingCompleted => null,
            ContactUpdated => null,
            ContactDeleted => null,
            _ => "Unknown"
        };
    }

    private string GetName() => Type switch
    {
        MessageReceived => $"SMS Received from {Data.DisplayFrom} to {Data.DisplayTo}",
        MessageDelivered => $"SMS Sent to {Data.DisplayTo} from {Data.DisplayFrom}",
        CallRinging => Data.Direction switch
        {
            "Incoming" => $"Incoming Call from {Data.DisplayFrom} to {Data.DisplayTo}: Ringing",
            "Outgoing" => $"Outgoing Call to {Data.DisplayTo} from {Data.DisplayFrom}: Ringing",
            _ => "Call Ringing",
        },
        CallCompleted => Data.Direction switch
        {
            "Incoming" => $"Incoming Call from {Data.DisplayFrom} to {Data.DisplayTo}: Completed",
            "Outgoing" => $"Outgoing Call to {Data.DisplayTo} from {Data.DisplayFrom}: Completed",
            _ => "Call Ringing",
        },
        CallRecordingCompleted => $"Call Recording from {Data.DisplayFrom} to {Data.DisplayTo}",
        ContactUpdated => $"Contact {Data.FullNameWithCompany}: Updated",
        ContactDeleted => $"Contact {Data.FullNameWithCompany}: Deleted",
        _ => "Unknown"
    };
}