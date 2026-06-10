using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;

namespace PI.Shared.Filters;

public class ExceptionFilter : IExceptionFilter // , IAsyncExceptionFilter
{
    private readonly ILogger<ExceptionFilter> _logger;

    public ExceptionFilter(ILogger<ExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled) return;

        context.ExceptionHandled = true;

        var requestException = context.Exception as IHttpRequestException;
        var statusCode = requestException?.StatusCode ?? 500;

        Actor actor;
        try
        {
            actor = context.HttpContext.GetContextWithActor()?.Actor;
        }
        catch (Exception ex)
        {
            actor = null;
            _logger.LogError(ex, "Failed to get actor");
        }

        var value = new
        {
            StatusCode = statusCode,
            context.Exception.Message,
            Success = false,
            Actor = actor,
        };

        if (requestException == null)
        {
            _logger.LogError(context.Exception, "Unexpected Exception");
        }
        else
        {
            _logger.LogInformation(context.Exception, "Request Exception: {StatusCode}", requestException.StatusCode);
        }

        context.Result = new ObjectResult(value)
        {
            StatusCode = statusCode 
        };
    }
}