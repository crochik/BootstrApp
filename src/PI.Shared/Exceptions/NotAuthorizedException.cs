using System;
using PI.Shared.Models;

namespace PI.Shared.Exceptions;

public class NotAuthorizedException : Exception, IHttpRequestException
{
    public IEntityContext Context { get; }

    public int? StatusCode => 401;         

    public NotAuthorizedException(IEntityContext context, string message = null) : base(message)
    {
        Context = context;
    }

    public NotAuthorizedException()
    {
    }

    public NotAuthorizedException(string message) : base(message)
    {
    }
}