using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Controllers;

[ApiController]
[Produces("application/json")]
public class APIController : ControllerBase
{
    protected IContextWithActor Context => HttpContext.GetContextWithActor();
    protected AbstractAPIActor Actor => Context.Actor as AbstractAPIActor;

    protected void Prepare(DataViewRequest request)
    {
        if (Request.Headers.TryGetValue("Accept", out var headers))
        {
            request.ContentType = headers.FirstOrDefault();
        }
    }

    protected DataViewRequest Prepare(IDataView dataView, DataViewRequest request)
    {
        Prepare(request);

        // use query parameters to build request
        if (Request.Query?.Count > 0 && dataView.DataView.FilterForm?.Fields.Length > 0)
        {
            var criteria = request?.Criteria.ToList() ?? new List<Condition>();
            var filterFields = dataView.DataView.FilterForm.Fields.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var query in Request.Query)
            {
                if (filterFields.TryGetValue(query.Key, out var field))
                {
                    if (!criteria.Any(x => x.FieldName == field.Name))
                    {
                        criteria.Add(new Condition
                        {
                            FieldName = field.Name,
                            Operator = Operator.Eq,
                            Value = query.Value
                        });
                    }
                }
            }

            if (criteria.Count > 0)
            {
                request ??= new DataViewRequest();
                request.Criteria = criteria.ToArray();
            }
        }

        return request;
    }

    /// <summary>
    /// Seed buildcontext with the request info
    /// </summary>
    /// <returns></returns>
    protected Dictionary<string, object> BuildRunContext()
    {
        var runContext = new Dictionary<string, object>
        {
            { "Context", Context.GetPlaceholders() },
        };

        if (Request.Query.Count > 0)
        {
            var req = new Dictionary<string, object>();
            foreach (var query in Request.Query)
            {
                req.Add(query.Key, query.Value.Count == 1 ? query.Value[0] : query.Value.ToArray());
            }

            runContext.TryAdd("Request|Parameters", req);
        }

        return runContext;
    }
}


/// <summary>
/// Can be used to force an action to return the object result formatted
/// using the default contract resolver
/// IT DOESN'T SEEM TO AFFECT THE swagger generator :(
/// </summary>
public class DefaultContractJsonFilterAttribute : ActionFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult objectResult)
        {
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver()
            };
            
            var jsonFormatter = new NewtonsoftJsonOutputFormatter(
                serializerSettings, 
                ArrayPool<char>.Shared,
                new MvcOptions
                {
                    
                },
                null);

            objectResult.Formatters.Add(jsonFormatter);
        }

        base.OnResultExecuting(context);
    }
}