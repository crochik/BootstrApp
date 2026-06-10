using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Crochik.Dipper;

[DiscriminatorWithFallback]
[BsonKnownTypes(
    typeof(UpdateStoredProcedure),
    typeof(AggregateStoredProcedure),
    typeof(MacroStoredProcedure)
)]
[BsonCollection("Dipper.StoredProcedures")]
[BsonDiscriminator(Required = true)]
public abstract class StoredProcedure
{
    public const string ObjectTypeFullName = "dipper.StoredProcedure";
    
    [BsonId]
    public string Id { get; set; }
    public string Collection { get; set; }

    /// <summary>
    /// Database Name when different than the connection's default
    /// NOT USED YET!
    /// </summary>
    public string DatabaseName { get; set; }

    private string _name;
    public string Name
    {
        get => _name ?? (Id?.StartsWith(Namespace + ".") == true ? Id.Substring(Namespace.Length + 1) : null);
        set => _name = value;
    }

    public string Description { get; set; }
    public Parameter[] Parameters { get; set; }

    [BsonElement("Namespace")]
    public string Namespace => Id?.Split('.')[0];

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedOn { get; set; }
    
    // LastActor?
    // ...
    
    public int Version { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public Guid? AccountId { get; set; }
    
    public abstract string Body { get; }

    public abstract string ToString(IDictionary<string, object> parameters);
    public abstract Task<object> ExecuteAsync(MongoConnection connection, IDictionary<string, object> parameters);

    public bool HasAllParameters(IDictionary<string, object> parameters)
    {
        if (Parameters == null) return true;

        var reqParams = (Parameters ?? Enumerable.Empty<Parameter>())
            .Where(x => x.DefaultValue == null)
            .Select(x => x.Name);

        foreach (var p in reqParams)
        {
            if (!parameters.TryGetValue(p, out var value)) return false;
        }

        return true;
    }

    protected BsonDocument[] Apply(IEnumerable<string> pipeline, IDictionary<string, object> parameters)
    {
        var docs = pipeline.Select(x => BsonDocument.Parse(x).ReplaceFunctions()).ToArray();

        foreach (var p in Parameters ?? Array.Empty<Parameter>())
        {
            var value = parameters.TryGetValue(p.Name, out var tmp) ? tmp : null;
            value ??= p.DefaultValue;
            if (value == null && p.IsRequired) throw new DipperException($"{p.Name} is Required");

            var parameter = "{{{" + $"Parameters.{p.Name}" + "}}}";

            foreach (var doc in docs)
            {
                doc.Replace(parameter, BsonValue.Create(value));
            }
        }

        return docs;
    }
}

public static class StoredProcedureExtensions
{
    public static string GetFriendlyTypeName(this StoredProcedure sp)
    {
        return sp switch
        {
            UpdateStoredProcedure _ => "Update",
            MacroStoredProcedure _ => "Macro",
            AggregateStoredProcedure _ => "Aggregate",
            _ => "Unknown"
        };
    }
}