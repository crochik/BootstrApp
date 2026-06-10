using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using PI.Shared.Attributes;
using PI.Shared.ContractResolvers;
using PI.Shared.Json;

namespace PI.Shared.Filters;

public class DynamicJsonSerializationFilter : IActionFilter
{
    private static readonly JsonSerializerSettings ToJsonResultSettings = new()
    {
        ContractResolver = new ApiNamePropertyNameContractResolver
        {
            // OverrideIgnoreAttribute = false,
        },
        NullValueHandling = NullValueHandling.Ignore,
        Converters =
        [
            new FlagsEnumConverter(),
            new DefaultEnumJsonConverter(),
            new Decimal128Converter(), // force decimal128 to be serialized as numbers
            new ObjectIdConverter(), // will serialize objectids as UUIDs strings
        ]
    };

    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is not ObjectResult objectResult)
        {
            // not object result, do nothing
            return;
        }

        var request = context.HttpContext.Request;

        // only when accepts json or not specified
        var accept = request.Headers.Accept;
        var isJsonRequested = accept.IsEmpty() || accept.Contains("application/json") || accept.Contains("*/*");
        if (!isJsonRequested)
        {
            // does not accept json, do nothing
            return;
        }

        // TODO: could allow client to override api but...
        // for now check independently, only as opt-in to non-standard behavior 

        // action attribute [UseApiNamesAttribute]
        var endpoint = context.HttpContext.GetEndpoint();
        var hasAttribute = endpoint?.Metadata.GetMetadata<UseApiNamesAttribute>() != null;
        if (hasAttribute)
        {
            // override 
            context.Result = new JsonResult(objectResult.Value, ToJsonResultSettings);
            return;
        }

        // opt in header
        // if (request.Headers.ContainsKey("X-Api-Names"))
        // {
        //     // override 
        //     context.Result = new JsonResult(objectResult.Value, ToJsonResultSettings);
        //     return;
        // }

        // client_json claim 
        // var user = context.HttpContext.User;
        // var includesClaim = user.HasClaim(c => c is { Type: "client_json", Value: "unmodified" });
        // if (includesClaim)
        // {
        //     // override 
        //     context.Result = new JsonResult(objectResult.Value, ToJsonResultSettings);
        // }
    }
}