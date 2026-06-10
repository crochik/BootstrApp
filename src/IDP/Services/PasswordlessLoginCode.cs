using System;
using Crochik.Mongo;
using PI.Shared.Models;

namespace Services;

[BsonCollection("idp.PasswordlessLoginCode")]
public class PasswordlessLoginCode : FlowObjectModel
{
    public const string ObjectTypeFullName = "idp.PasswordlessLoginCode";

    public override string ObjectType => ObjectTypeFullName;
    
    public string ClientId { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Pin { get; set; }
    public string CodeChallenge { get; set; }
    public string CodeChallengeMethod { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool RequestedSMS { get; set; }
}