using System;
using System.IO;
using lib.common;
using log4net;
using Tarczynski.NtpDateTime;

namespace lib.core
{
    public class TimeProvider
    {
        private static long _offset = 0;
        private static object _offsetLock = new object();
        private static ILog LOGGER = LogManager.GetLogger(typeof(TimeProvider));
        private static Method _method = Method.NTP;

        private TimeProvider()
        {
        }

        public static void init(Session.Configuration conf)
        {
            switch (_method = conf.timeSynchronizationMethod)
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
                    lock (_offsetLock)
                    {
                        _offset = conf.timeManualCorrection;
                    }
                    break; 
            }
        }

        public static void init(Session session)
        {
            if (_method != Method.MELODY) return;

            updateMelody(session);
        }

        public static long currentTimeMillis()
        {
            lock (_offsetLock) { 
                return Utils.getUnixTimeStampInMilliseconds() + _offset;
            }
        }

        /// <exception cref="IOException" />
        private static void updateWithNtp()
        {
            lock (_offsetLock)
            {
                DateTime time = DateTime.Now.FromNtp();
                int offsetValue = TimeZone.CurrentTimeZone.GetUtcOffset(time).Milliseconds;
                LOGGER.Debug("Loaded time offset from NTP: " + offsetValue + "ms");
                _offset = offsetValue;
            }
        }

        private static void updateMelody(Session session)
        {
            // Needs the whole api class implemented
            //ToDo: Implement 
            throw new NotImplementedException();
        }

        public static void updateWithPing(byte[] pingPayload)
        {
            if (_method != Method.PING) return;

            lock (_offsetLock)
            {
                byte[] fourBytes = new byte[4];
                Buffer.BlockCopy(pingPayload, 0, fourBytes, 0, 4);
                int pingInt = BitConverter.ToInt32(fourBytes, 0);
                long diff = pingInt * 1000L - Utils.getUnixTimeStampInMilliseconds();
                _offset = diff;
                
                LOGGER.Debug("Loaded time offset from ping: " + diff + "ms");
            }
        }
        
        public enum Method {
            NTP, PING, MELODY, MANUAL
        }
    }
}