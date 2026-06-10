using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.OpenAPI.Controllers;

[Microsoft.AspNetCore.Components.Route("/openapi/v1/[controller]")]
public class ObjectTypeController(ILogger<ObjectTypeController> logger, MongoConnection connection) : APIController
{
    /// <summary>
    /// Export object type to yaml format 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("{objectTypeName}/Export")]
    public async Task<IActionResult> ExportObjectType([FromRoute] string objectTypeName)
    {
        var doc = await connection.Filter<BsonDocument>("ObjectType.1")
            .Eq(nameof(ObjectType.FullName), objectTypeName)
            .FirstOrDefaultAsync();

        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson };
        var json = doc.ToJson(settings);

        var rootNode = JsonNode.Parse(json);
        var sorted = JsonSorter.Sort(rootNode);

        // var options = new JsonSerializerOptions { WriteIndented = true };
        // json = sorted.ToJsonString(options);
        // return Content(json, "application/json");

        var yaml = Yamler.ToString(sorted);
        return Content(yaml, "application/yaml");
    }

    /// <summary>
    /// Create missing field indices  
    /// </summary>
    [Authorize("admin")]
    [HttpPost("{objectTypeName}/Indices")]
    public async Task<IActionResult> GetIndicesAsync([FromRoute] string objectTypeName, [FromServices] ObjectTypeService service, [FromQuery] bool deleteExisting, [FromQuery] bool createMissing)
    {
        var objectType = await service.GetAsync(Context, objectTypeName);
        if (objectType == null) throw NotFoundException.New(objectTypeName);
        if (string.IsNullOrEmpty(objectType.CollectionName) || objectType.IsEmbedded) throw new BadRequestException("Invalid ObjectType");

        var indexedFields = objectType.Fields.Values
            .Where(x => x.Indexed)
            .ToDictionary(x => FormField.GetPathInCollection(x.Field.Name), x => x.Field);

        var missing = indexedFields.Keys.ToHashSet();
        var found = new Dictionary<string, Index>();
        var notStandard = new List<Index>();

        string[] constrainedFields = [nameof(Model.AccountId)];

        // "hard constraints" 
        if (objectType.Constraints.TryGetValue(nameof(EntityRoleId.Account), out var constraints))
        {
            constrainedFields = constraints.Conditions.Select(x => x.FieldName).Distinct().ToArray();
        }

        var collection = connection.Database.GetCollection<BsonDocument>(objectType.CollectionName);
        using (var cursor = await collection.Indexes.ListAsync())
        {
            while (await cursor.MoveNextAsync())
            {
                foreach (var i in cursor.Current)
                {
                    var name = i.GetValue("name").AsString;
                    var keys = i.GetValue("key").AsBsonDocument.Elements.Select(x => new IndexKey { Name = x.Name, Value = x.Value }).ToArray();
                    var index = new Index
                    {
                        Name = name,
                        Keys = keys,
                    };

                    // TODO: exclude any field in the Account constraints
                    // right now assumes AccountId
                    // ...

                    if (keys.Length < constrainedFields.Length + 1)
                    {
                        notStandard.Add(index);
                        continue;
                    }

                    var piIndex = name.StartsWith("PI_");
                    if (!piIndex && (keys.Length == 2 && constrainedFields.Length == 1 && keys[0].Name == constrainedFields[0]))
                    {
                        // check keys
                        piIndex = true;
                        for (var c = 0; c < constrainedFields.Length; c++)
                        {
                            if (keys[c].Name != constrainedFields[c])
                            {
                                piIndex = false;
                                break;
                            }
                        }
                    }

                    if (piIndex)
                    {
                        logger.LogInformation("Existing PI Index: {Name}", name);

                        if (deleteExisting)
                        {
                            // delete index
                            logger.LogWarning("Delete Index: {Name}", name);
                            await collection.Indexes.DropOneAsync(name);
                        }
                        continue;
                    }

                    if (missing.Remove(index.Keys[1].Name))
                    {
                        found[index.Keys[1].Name] = index;
                        continue;
                    }

                    notStandard.Add(index);
                }
            }
        }

        if (createMissing)
        {
            foreach (var fieldName in missing)
            {
                if (fieldName == Model.IdFieldName) continue;
                if (constrainedFields.Contains(fieldName)) continue;
                if (!indexedFields.TryGetValue(fieldName, out var field)) continue;
                if (!(field switch
                    {
                        ChildrenField => false,
                        ObjectField => false,
                        DictionaryField => false,
                        LocationField => false, // ???
                        LocationDistanceField => false, // ????
                        RelatedObjectsField => false,
                        LookupField => false,
                        _ => true,
                    })) continue;

                logger.LogInformation("Create Index for {ObjectTypeName}.{FieldName}", objectType.Name, fieldName);

                var keys = Builders<BsonDocument>.IndexKeys.Ascending(constrainedFields[0]);
                for (var c = 1; c < constrainedFields.Length; c++)
                {
                    keys = keys.Ascending(constrainedFields[c]);
                }

                keys = keys.Ascending(fieldName);

                await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions
                {
                    Name = string.Join("_", constrainedFields.Prepend("PI").Append(fieldName)).Replace('.', '|'),
                    Background = true,
                }));
            }
        }

        return Ok(new
        {
            Found = found,
            Other = notStandard,
            Missing = missing,
        });
    }

    public class Index
    {
        public string Name { get; set; }
        public IndexKey[] Keys { get; set; }
    }

    public class IndexKey
    {
        public string Name { get; set; }
        public object Value { get; set; }
    }
}