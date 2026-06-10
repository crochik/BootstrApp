using System;
using Crochik.Mongo;

namespace PI.Shared.Models.OpenAPI;

[BsonCollection("openapi.Document")]
public class Document : EntityOwnedModel
{
    public string Namespace { get; set; }
    public string BaseUrl { get; set; }
    public Guid? IntegrationId { get; set; }
    
    public Guid? RemoteFileId { get; set; }
}