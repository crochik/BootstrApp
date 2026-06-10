using System.Collections.Generic;
using Microsoft.OpenApi;

namespace PI.Shared.Services.OpenApiGenerator;

public static class OpenApiOperationExtensions
{
    public static OpenApiOperation AddParameter(this OpenApiOperation operation, OpenApiParameter parameter)
    {
        operation.Parameters ??= new List<IOpenApiParameter>();
        operation.Parameters.Add(parameter);

        return operation;
    }

}