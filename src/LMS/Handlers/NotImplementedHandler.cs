using System;
using System.Threading.Tasks;
using LMS.Models;

namespace LMS.Handlers;

public class NotImplementedHandler : INewLeadHandler
{
    public Func<Context, ValueTask<Response>> Build(Func<Context, ValueTask<Response>> next) => context => ValueTask.FromResult(new Response
    {
        Reason = "NOT_READY",
        Message = "Not Implemented",
    });
}