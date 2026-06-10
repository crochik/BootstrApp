using System.Collections.Generic;
using Crochik.Mongo;
using PI.Shared.Models;

namespace PI.LangChain.Models;

// public class DocumentTemplate : IDocumentTemplate
// {
//     public Dictionary<string, string> StoredProcedures { get; }
//     public string Template { get; }
//
//     public DocumentTemplate(string template, Dictionary<string, string> storedProcedures)
//     {
//         Template = template;
//         StoredProcedures = storedProcedures;
//     }
// }

/// <summary>
/// Template to generate context document
/// </summary>
[BsonCollection("ai.DocumentTemplate")]
public class DocumentTemplateProfileElement : AppProfileElement, IDocumentTemplate
{
    /// <summary>
    /// Stored procedures used, key is the "alias" for the helper function
    /// </summary>
    public Dictionary<string, string> StoredProcedures { get; set; }
    
    /// <summary>
    /// Handlebars template
    /// </summary>
    public string Template { get; set; }
}