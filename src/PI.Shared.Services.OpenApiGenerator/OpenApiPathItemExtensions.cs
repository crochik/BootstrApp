using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.OpenApi;

namespace PI.Shared.Services.OpenApiGenerator;

public static class OpenApiPathItemExtensions
{
    public static OpenApiPathItem AddOperation(this OpenApiPathItem pathItem, string method, OpenApiOperation operation)
    {
        pathItem.Operations ??= new Dictionary<HttpMethod, OpenApiOperation>();
        pathItem.Operations[GetHttpMethod(method)] = operation;
        return pathItem;
    }

    private static HttpMethod GetHttpMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "OPTIONS" => HttpMethod.Options,
            "HEAD" => HttpMethod.Head,
            "POST" => HttpMethod.Post,
            "TRACE" => HttpMethod.Trace,
            _ => throw new NotImplementedException($"{method} not recognized"),
        };
    }
}