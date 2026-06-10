using System;
using System.Linq;
using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PI.Shared.OpenAPI;

public class ExplicitAddClassesDocumentFilter<T> : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // only add the extra classes to the API (main) doc
        if (context.DocumentName != "API") return;
        
        RegisterSubClasses(context, typeof(T));
    }
    
    private static void RegisterSubClasses(DocumentFilterContext context, Type baseType)
    {
        var schemaGenerator = context.SchemaGenerator;
        
        // register all subclasses
        var derivedTypes = baseType.GetTypeInfo().Assembly.GetTypes()
            .Where(x => baseType != x && baseType.IsAssignableFrom(x));
    
        schemaGenerator.GenerateSchema(baseType, context.SchemaRepository);
        
        foreach (var type in derivedTypes)
            schemaGenerator.GenerateSchema(type, context.SchemaRepository);
    }    
}