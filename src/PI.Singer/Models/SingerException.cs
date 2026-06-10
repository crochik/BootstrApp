using System;

namespace Models
{
    public class SingerException : Exception
    {
        public SingerException(string message) : base(message)
        {
        }

        public SingerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}