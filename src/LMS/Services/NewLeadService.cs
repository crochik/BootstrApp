using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMS.Handlers;
using LMS.Models;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Services;

public class NewLeadService
{
    private readonly IEnumerable<INewLeadHandler> _handlers;
    private readonly IResponseWriter _responseWriter;

    public NewLeadService(IEnumerable<INewLeadHandler> handlers, IResponseWriter responseWriter)
    {
        _handlers = handlers;
        _responseWriter = responseWriter;
    }

    public async Task<ActionResult> HandleAsync(Request request)
    {
        var context = new Context
        {
            Request = request,
        };

        var handler = _handlers
            .Reverse()
            .Aggregate<INewLeadHandler, Func<Context, ValueTask<Response>>>(null, (current, handler) => handler.Build(current));

        var result = await handler(context);

        return _responseWriter.Write(context, result);
    }
}