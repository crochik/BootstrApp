using System;
using Microsoft.OpenApi;

namespace PI.Shared.Services.OpenApiGenerator;

public static class OpenApiParameterExtensions
{
    public static OpenApiParameter WithIn(this OpenApiParameter parameter, string placement)
    {
        if (!Enum.TryParse<ParameterLocation>(placement, out var location)) throw new Exception("Unexpected parameter location");
        parameter.In = location;
        return parameter;
    }
}