using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PI.Shared.OpenAPI;

// https://stackoverflow.com/questions/41141137/how-can-i-tell-swashbuckle-that-the-body-content-is-required
public class StringBodyAttribute : Attribute
{
    public StringBodyAttribute(string mediaType = "text/plain")
    {
        ParameterName = "payload";
        Required = true;
        MediaType = mediaType;
    }

    public string MediaType { get; set; }
    public bool Required { get; set; }
    public string ParameterName { get; set; }
}

public class StringBodyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<StringBodyAttribute>().FirstOrDefault();

        if (attribute == null)
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                {attribute.MediaType, new OpenApiMediaType()
                {
                    Schema = new OpenApiSchema()
                    {
                        Type = JsonSchemaType.String,
                    },
                }}
            },
        };
    }
}