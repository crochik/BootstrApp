using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

public interface IDocumentTemplate
{
    Dictionary<string, string> StoredProcedures { get; }
    string Template { get; }
}

[BsonCollection("report.DocumentTemplate")]
public class DocumentTemplate : FlowObjectModel, IDocumentTemplate
{
    public string ContentType { get; set; }
    public required string Template { get; set; }
    public required Dictionary<string, string> StoredProcedures { get; set; }

    public Dictionary<string, string> Inputs { get; set; }
}