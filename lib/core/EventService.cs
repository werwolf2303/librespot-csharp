using System;
using System.IO;
using System.Text;
using csharp;
using lib.common;
using lib.mercury;
using log4net;

namespace lib.core
{
    public class EventService : IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(EventService));
        private AsyncWorker<EventBuilder> _asyncWorker;
        
        internal EventService(Session session)
        {
            _asyncWorker = new AsyncWorker<EventBuilder>("event-service-sender", EventBuilder =>
            {
                try
                {
                    byte[] body = EventBuilder.ToArray();
                    RawMercuryRequest.Builder mercuryRequestBuilder = RawMercuryRequest.NewBuilder();
                    mercuryRequestBuilder.Uri = "hm://event-service/v1/events";
                    mercuryRequestBuilder.Method = "POST";
                    mercuryRequestBuilder.AddUserField("Accept-Language", "en");
                    mercuryRequestBuilder.AddUserField("X-ClientTimeStamp", TimeProvider.currentTimeMillis().ToString());
                    mercuryRequestBuilder.AddPayloadPart(body);
                    MercuryClient.Response resp = session.GetMercury().SendSync(mercuryRequestBuilder.Build());

                    LOGGER.DebugFormat("Event sent. (body: {0}, result: {1})", EventBuilder.ToString(body), resp.StatusCode);
                }
                catch (IOException ex)
                {
                    LOGGER.Error("Failed sending event: " + EventBuilder, ex);
                }
            });
        }
        
        public void SendEvent(GenericEvent genericEvent) {
            SendEvent(genericEvent.Build());
        }

        public void SendEvent(EventBuilder builder)
        {
            _asyncWorker.Submit(builder);
        }

        public void Dispose()
        {
            _asyncWorker.Dispose();
        }

        public class Type : Enumeration
        {
            public static readonly Type NewSessionId = new Type("557", "3");
            public static readonly Type NewPlaybackId = new Type("558", "1");
            public static readonly Type TrackTransition = new Type("12", "38");

            public String Id;
            public String Unknown;

            private Type(String id, String unknown)
            {
                Id = id;
                Unknown = unknown;
            }
        }

        public interface GenericEvent
        {
            EventBuilder Build();
        }

        public class EventBuilder
        {
            private MemoryStream _memoryStream = new MemoryStream(256);
            private BinaryWriter _body;

            public EventBuilder(Type type)
            {
                _body = new BinaryWriter(_memoryStream);
                
                AppendNoDelimiter(type.Id);
                Append(type.Unknown);
            }

            internal string ToString(byte[] body)
            {
                StringBuilder result = new StringBuilder();
                foreach (byte b in body)
                {
                    if (b == 0x09) result.Append('|');
                    else result.Append((char)b);
                }
                
                return result.ToString();
            }

            private void AppendNoDelimiter(String str)
            {
                if (str == null) str = "";
                
                _body.Write(Encoding.UTF8.GetBytes(str));
            }

            public EventBuilder Append(char c)
            {
                _body.Write((byte) 0x09);
                _body.Write(c);
                return this;
            }

            public EventBuilder Append(String str)
            {
                _body.Write((byte) 0x09);
                AppendNoDelimiter(str);
                return this;
            }

            public override string ToString()
            {
                return "EventBuilder(" + ToString(ToArray()) + ")";
            }

            internal byte[] ToArray()
            {
                return _memoryStream.ToArray();
            }
        }
    }
}