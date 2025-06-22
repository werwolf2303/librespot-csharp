using System;
using System.IO;
using log4net;
using Tarczynski.NtpDateTime;

namespace lib.core
{
    public class TimeProvider
    {
        private static long offset = 0;
        private static object offsetLock = new object();
        private static ILog LOGGER = LogManager.GetLogger(typeof(TimeProvider));
        private static Method method = Method.NTP;

        private TimeProvider()
        {
        }

        public static void init(Session.Configuration conf)
        {
            switch (method = conf.timeSynchronizationMethod)
            {
                case Method.NTP:
                    try
                    {
                        updateWithNtp();
                    }
                    catch (IOException ex)
                    {
                        LOGGER.Warn("Failed updating time!", ex);
                    }
                    break;
                case Method.MANUAL:
                    lock (offsetLock)
                    {
                        offset = conf.timeManualCorrection;
                    }
                    break; 
            }
        }

        public static void init(Session session)
        {
            if (method != Method.MELODY) return;

            updateMelody(session);
        }

        public static long currentTimeMillis()
        {
            lock (offsetLock) { 
                return DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }
        }

        /// <exception cref="IOException" />
        private static void updateWithNtp()
        {
            lock (offsetLock)
            {
                DateTime time = DateTime.Now.FromNtp();
                int offsetValue = TimeZone.CurrentTimeZone.GetUtcOffset(time).Milliseconds;
                LOGGER.Debug("Loaded time offset from NTP: " + offsetValue + "ms");
                offset = offsetValue;
            }
        }

        private static void updateMelody(Session session)
        {
            //ToDo: Implement 
            throw new NotImplementedException();
        }

        private static void updateWithPing(byte[] pingPayload)
        {
            if (method != Method.PING) return;
            
            // synchronized (offset) {
            throw new NotImplementedException();
            // }
        }
        
        public enum Method {
            NTP, PING, MELODY, MANUAL
        }
    }
}