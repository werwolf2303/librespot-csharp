using System;

namespace deps.NtpDateTime
{
    public static class NtpDateTimeExtensions
    {
        public static DateTime FromNtp(this DateTime input, bool computerTimeOnException = true)
        {
            return NtpDateTime.Now(computerTimeOnException);
        }
    }
}