using System.Collections.Generic;
using Microsoft.OpenApi;

namespace PI.Shared.Services.OpenApiGenerator;

public static class OpenApiDocumentExtensions
{
    public static OpenApiDocument AddOperation(this OpenApiDocument document, string path, string method, OpenApiOperation operation)
    {
        if (!document.Paths.TryGetValue(path, out var pathItem))
        {
            pathItem = new OpenApiPathItem();
            document.Paths[path] = pathItem;
        }

        ((OpenApiPathItem)pathItem).AddOperation(method, operation);
        
        return document;
    }
    
    public static OpenApiDocument AddSuccessResponse(this OpenApiDocument document, OpenApiOperation operation, string schemaId)
    {
        operation.Responses ??= new  OpenApiResponses();
        operation.Responses.Add("200", new OpenApiResponse
        {
            Description = "OK",
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchemaReference(schemaId, document)
                }
            }
        });

        return document;
    }
    
    public static OpenApiDocument AddSuccessArrayResponse(this OpenApiDocument document, OpenApiOperation operation, string schemaId)
    {
        operation.Responses ??= new  OpenApiResponses();
        operation.Responses.Add("200", new OpenApiResponse
        {
            Description = "OK",
            Content = new Dictionary<string, IOpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = new OpenApiSchemaReference(schemaId, document),
                    }
                }
            }
        });

        return document;
    }    
}