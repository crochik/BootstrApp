using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.Google.Models;

[BsonCollection("google.CloudMessageRegistration")]
public class CloudMessageRegistration : FlowObjectModel
{
    public string Token { get; set; }
    public string ClientId { get; set; }
}