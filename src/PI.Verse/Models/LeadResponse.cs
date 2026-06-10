using System;
using Newtonsoft.Json;

namespace Models;

public class VerseResponse
{
    public Guid? Id { get; set; }

    [JsonProperty("request_id")]
    public Guid? RequestId { get; set; }

    public Guid? Attempt { get; set; }

    public string Status { get; set; }
}

public class UpdateLeadRequest
{
    public const string Status_Working = "working";
    public const string Status_Qualified = "qualified";
    public const string Status_Unqualified = "unqualified";

    public string Status { get; set; }
    public string Notes { get; set; }
    public string Reason { get; set; }
    public DateTime? AppointmentDate { get; set; }
}