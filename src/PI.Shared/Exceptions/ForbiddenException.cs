using System;
using PI.Shared.Models;

namespace PI.Shared.Exceptions
{
    public class ForbiddenException : Exception, IHttpRequestException
    {
        public IEntityContext Context { get; }

        public int? StatusCode => 403;         

        public ForbiddenException(IEntityContext context, string message = null) : base(message)
        {
            Context = context;
        }

        public ForbiddenException()
        {
        }

        public ForbiddenException(string message) : base(message)
        {
        }

    }
}