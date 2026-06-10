using System.Collections.Generic;
using System.Linq;

namespace Services;

public abstract class OpenApiSchema
{
    public string Name { get; set; }
    public string Description { get; set; }
    public abstract string Type { get; }
}

public class StringSchema : OpenApiSchema
{
    public override string Type => "string";
}

public class ObjectSchema : OpenApiSchema
{
    public override string Type => "object";

    public bool? AdditionalProperties { get; } = false;
    public Dictionary<string, OpenApiSchema> Properties { get; set; }
    public string[] Required => Properties?.Keys.ToArray();
}

public class EnumSchema : StringSchema
{
    public string[] Enum { get; set; }
}

public class RootSchema : ObjectSchema
{
    public bool Strict { get; set; } = true;
}