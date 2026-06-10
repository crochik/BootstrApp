using System;
using System.Collections.Generic;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;

namespace LMS.Models;

// TODO: this will be an issue with we have two accounts getting the same cert 
// ...
[BsonCollection("lms.Certificate")]
[BsonDiscriminator(Required = true)]
[DiscriminatorWithFallback]
[BsonKnownTypes(typeof(TrustedFormCertificate))]
public class Certificate
{
    public Guid AccountId { get; set; }
    
    [BsonId]
    public string Id { get; set; }
    
    public DateTime CreatedOn { get; set; }
    
    public Guid? LeadId { get; set; }
}

[BsonDiscriminator("trustedform")]
public class TrustedFormCertificate : Certificate
{
    public bool Validated { get; set; }
    public bool Retained { get; set; }
    public Dictionary<string,object> Insights { get; set; }
}