using System;

namespace PI.Shared.Exceptions
{
    public class BadRequestException : Exception, IHttpRequestException
    {
        public int? StatusCode => 400;

        public BadRequestException(string message) : base(message)
        {
        }
    }
}