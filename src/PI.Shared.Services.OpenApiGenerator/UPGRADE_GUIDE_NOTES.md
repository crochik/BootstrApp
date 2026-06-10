# OpenAPI.NET 2.0 Upgrade Guide - Key Changes

## Type Property Change
- **v1.x**: `Type = "string"` (string value)
- **v2.0**: `Type = JsonSchemaType.String` (enum)
- For nullable: `Type = JsonSchemaType.String | JsonSchemaType.Null`

## JsonSchemaType Enum Values
- JsonSchemaType.String
- JsonSchemaType.Number
- JsonSchemaType.Integer
- JsonSchemaType.Boolean
- JsonSchemaType.Object
- JsonSchemaType.Array
- JsonSchemaType.Null

## References
- Discriminator mappings now use `OpenApiSchemaReference` instead of strings
- Schema references need to use specialized reference types
- References support Summary and Description in OpenAPI 3.1

## Collections
- Collections NO LONGER auto-initialize
- Must use `??=` or explicit initialization before adding items
- Example: `mySchema.AnyOf ??= new List<IOpenApiSchema>();`

## OpenApiAny Types → JsonNode
- OpenApiString → JsonValue.Create(string) or just string
- OpenApiInteger → JsonValue.Create(int) or just int
- OpenApiBoolean → bool
- OpenApiArray → JsonArray
- IOpenApiAny → JsonNode

## Async Methods
- SerializeAsJson() → SerializeAsJsonAsync()
- All I/O operations now async

## Namespaces
- Microsoft.OpenApi.Models → Microsoft.OpenApi
- Microsoft.OpenApi.Any → removed (use System.Text.Json.Nodes)

## HTTP Methods
- OperationType.Get → HttpMethod.Get
- Using System.Net.Http.HttpMethod instead of OperationType enum
