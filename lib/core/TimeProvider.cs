using System;
using System.IO;
using System.Net;
using deps.HttpSharp;
using deps.NtpDateTime;
using lib.common;
using lib.dealer;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;

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
            switch (_method = conf.TimeSynchronizationMethod)
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
                        _offset = conf.TimeManualCorrection;
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
            try
            {
                HttpResponse resp = session.GetApi().Send(ApiClient.RequestMethod.OPTIONS, "/melody/v1/time");
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    LOGGER.ErrorFormat("Failed notifying server of time request! (code: {0}, msg: {1})",
                        (int)resp.StatusCode, resp.GetResponseString());
                    return;
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is MercuryClient.MercuryException)
                {
                    LOGGER.Error("Failed notifying server of time request!", ex);
                    return;
                }

                throw;
            }

            try
            {
                HttpResponse resp = session.GetApi().Send(ApiClient.RequestMethod.GET, "/melody/v1/time");
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    LOGGER.ErrorFormat("Failed requesting time! (code: {0}, msg: {1})",
                        (int)resp.StatusCode, resp.GetResponseString());
                    return;
                }

                if (resp.GetResponseStream() == null)
                    throw new Exception("Illegal state!");

                JObject obj = JObject.Parse(resp.GetResponseString());
                long diff = obj["timestamp"].ToObject<long>() - Utils.getUnixTimeStampInMilliseconds();
                lock (_offsetLock)
                {
                    _offset = diff;
                }

                LOGGER.Info("Loaded time offset from melody: " + diff + "ms");
            }
            catch (Exception ex)
            {
                LOGGER.Error("Failed requesting time!", ex);
            }
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