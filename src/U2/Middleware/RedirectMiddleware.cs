using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using PI.Shared.Middleware;
using PI.Shared.Models.U2;

namespace PI.U2.Middleware;

public class RedirectMiddleware
{
    private readonly ILogger<RedirectMiddleware> _logger;
    private readonly MongoConnection _connection;
    private readonly RequestDelegate _next;

    public RedirectMiddleware(ILogger<RedirectMiddleware> logger, MongoConnection connection, RequestDelegate next)
    {
        _logger = logger;
        _connection = connection;
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<RequestContextMiddleware> logger, IWebHostEnvironment env)
    {
        using var scope = _logger.AddScope(new
        {
            context.Request.Host, 
            context.Request.QueryString,
        });

        var host = context.Request.Host.Value;
        var path = context.Request.Path.Value?.Split("/", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? new[] { string.Empty };
        
        var redirection = await _connection.Filter<ShortLinkRedirection>()
            .Eq(x => x.Host, host)
            .In(x => x.ShortCode, new[] { path[0], "*" })
            .Eq(x => x.IsActive, true)
            .SortDesc(x => x.ShortCode) // so wildcard is last
            .Update
            .Inc(x => x.ViewCount, 1)
            .Set(x => x.LastAccessedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (redirection == null)
        {
            if (context.Request.Path == "/health")
            {
                // health checks
                context.Response.StatusCode = 200;
                return;
            }
            
            _logger.LogError("No match found");
            // await DumpRequestAsync(context);
            context.Response.StatusCode = 404;
            return;
        }

        var location = redirection.Location;

        if (location.IndexOf("{{", StringComparison.Ordinal) > 0)
        {
            if (context.Request.Path.HasValue)
            {
                location = location.Replace("{{path}}", context.Request.Path.Value);
            }

            for (var c = 0; c < path.Length; c++)
            {
                location = location.Replace("{{path" + c + "}}", path[c]);
            }

            if (context.Request.QueryString.HasValue && context.Request.QueryString.Value !=null && context.Request.QueryString.Value.StartsWith("?"))
            {
                location = location.Replace("{{queryString}}", context.Request.QueryString.Value[1..]);
            }
            else
            {
                location = location.Replace("{{queryString}}", string.Empty);
            }
            
            foreach (var query in context.Request.Query)
            {
                location = location.Replace("{{" + query.Key + "}}", query.Value.FirstOrDefault());
            }
        }

        _logger.LogInformation("Redirect to {Location}: {ShortLinkRedirection}", location, redirection.Id);

        await _connection.InsertAsync(new RedirectionRequest
        {
            RedirectionId = redirection.Id,
            UserAgent = context.Request.Headers["User-Agent"],
            IpAddress = context.Request.Headers["X-Forwarded-For"], // context.Request.Headers["X-Real-IP"]
            RequestId = context.Request.Headers["X-Request-ID"],
            Location = location,
            Url = context.Request.GetDisplayUrl(),
            Query = context.Request.Query?.ToDictionary(x => x.Key, x => (object)(x.Value.Count == 1 ? x.Value.First() : x.Value.ToArray())),
        });
        
        context.Response.Redirect(location);
/*
<head>
  <meta http-equiv="Refresh" content="0; URL=https://example.com/" />
</head>
 */

// window.location = "https://example.com/";
    }

    private static async Task DumpRequestAsync(HttpContext context)
    {
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        await context.Response.WriteAsync("<html><head><title>U2</title></head>");
        await context.Response.WriteAsync("<body><div style=\"border: 1px solid red;\">");

        await context.Response.WriteAsync($"Scheme: {context.Request.Scheme}<br/>");
        await context.Response.WriteAsync($"Host: {context.Request.Host}<br/>");
        await context.Response.WriteAsync($"Path: {context.Request.Path}<br/>");
        await context.Response.WriteAsync($"QueryString: {context.Request.QueryString}<br/>");

        await context.Response.WriteAsync("<table>");
        foreach (var header in context.Request.Headers)
        {
            await context.Response.WriteAsync($"<tr><td>{header.Key}</td><td>");
            foreach (var value in header.Value) await context.Response.WriteAsync($"{value}</br>");
            await context.Response.WriteAsync("</td></tr>");
        }

        await context.Response.WriteAsync("</table>");

        await context.Response.WriteAsync("</div></body></html>");
    }
}