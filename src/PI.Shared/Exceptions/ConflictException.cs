using System;

namespace PI.Shared.Exceptions
{
    public class ConflictException : Exception, IHttpRequestException
    {
        public int? StatusCode => 409;

        public ConflictException(string message) : base(message)
        {
        }
    }
}