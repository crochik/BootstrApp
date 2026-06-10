using System.Collections.Generic;
using Crochik.Mongo;

namespace PI.Shared.Models;

[BsonCollection("ObjectType.Template")]
public class TemplateObject : AppProfileElement
{
    public string ObjectType { get; set; }
    public Dictionary<string, object> Object { get; set; }
}