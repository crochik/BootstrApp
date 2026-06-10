using System;

namespace PI.Shared.O365
{
    public class CalendarServiceException : Exception
    {
        public CalendarErrorCode ErrorCode { get; }

        public CalendarServiceException(CalendarErrorCode errorCode)
            : base(errorCode.ToString())
        {
            this.ErrorCode = errorCode;
        }

        public CalendarServiceException(CalendarErrorCode errorCode, string message)
            : base(message)
        {
            this.ErrorCode = errorCode;
        }

        public CalendarServiceException(string message)
            : base(message)
        {
            this.ErrorCode = CalendarErrorCode.Unspecified;
        }

    }
}