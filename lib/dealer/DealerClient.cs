using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using lib.common;
using lib.core;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities.Encoders;

namespace lib.dealer
{
    public class DealerClient
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(DealerClient));
        private AsyncWorker<Runnable.Run> _asyncWorker;
        private Session _session;
        private Dictionary<String, RequestListener> _reqListeners = new Dictionary<string, RequestListener>();
        private Dictionary<MessageListener, List<String>> _msgListeners =
            new Dictionary<MessageListener, List<string>>();
        private ScheduledExecutorService _scheduler = new ScheduledExecutorService();
        private volatile ConnectionHolder _conn;
        private ScheduledExecutorService.ScheduledFuture<dynamic> _lastScheduledReconnection;

        public DealerClient(Session session)
        {
            _session = session;
            _asyncWorker = new AsyncWorker<Runnable.Run>("dealer-worker", Run => { });
        }

        private static Dictionary<String, String> GetHeaders(JObject obj)
        {
            JObject headers = obj["headers"] as JObject;
            if (headers == null) return new Dictionary<string, string>();
            
            Dictionary<String, String> map = new Dictionary<String, String>();
            IEnumerator<KeyValuePair<String, JToken>> enumerator = headers.GetEnumerator();
            while (enumerator.MoveNext())
            {
                map.Add(enumerator.Current.Key, enumerator.Current.Value.ToString());
            }

            return map;
        }

        public void Connect()
        {
            lock (this)
            {
                _conn = new ConnectionHolder(_session,
                    String.Format("wss://{0}/?access_token={1}", _session.GetAPResolver().getRandomDealer(),
                        _session.GetTokens().Get("playlist-read")));
            }
        }
        
        private void WaitForListeners() {
            lock (_msgListeners)
            {
                if (_msgListeners.Count != 0) return;
                
                Monitor.Wait(_msgListeners);
            }
        }

        private void HandleRequest(JObject obj)
        {
            String mid = obj["message-Ident"].ToObject<string>();
            String key = obj["key"].ToObject<string>();

            Dictionary<String, String> headers = GetHeaders(obj);
            JObject payload = obj["payload"].ToObject<JObject>();
            if ("gzip".Equals(headers["Transfer-Encoding"]))
            {
                byte[] gzip = Base64.Decode(payload["compressed"].ToObject<string>());
                try
                {
                    GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress);
                    payload = JObject.Load(new JsonTextReader(new StreamReader(stream)));
                }
                catch (Exception ex)
                {
                    LOGGER.WarnFormat("Failed decompressing request! (mid: {0}, key: {1})", mid, key);
                    LOGGER.Error(ex);
                }
            }
            
            int pid = payload["message_id"].ToObject<int>();
            String sender = payload["sent_by_device_id"].ToObject<string>();
            
            JObject command = payload["command"].ToObject<JObject>();
            LOGGER.DebugFormat("Received request. (mid: {0}, key: {1}, pid: {2}, sender: {3}, command: {4})",
                mid, key, pid, sender, command);

            bool interesting = false;
            lock (_reqListeners)
            {
                foreach (String midPrefix in _reqListeners.Keys)
                {
                    if (midPrefix.StartsWith(mid))
                    {
                        RequestListener listener = _reqListeners[midPrefix];
                        interesting = true;
                        _asyncWorker.Submit((() =>
                        {
                            try
                            {
                                RequestResult result = listener.OnRequest(mid, pid, sender, command);
                                if (_conn != null) _conn.SendReply(key, result);
                                LOGGER.DebugFormat("Handled request. (key: {0}, result: {1})", key, result.ToString());
                            }
                            catch (Exception ex)
                            {
                                if (_conn != null) _conn.SendReply(key, RequestResult.UpstreamError);
                                LOGGER.ErrorFormat("Failed handling request. (key: {0})", key);
                                LOGGER.Error(ex);
                            }
                        }));
                    }
                }
            }
            
            if (!interesting) LOGGER.Debug("Couldn't dispatch request: " + mid);
        }

        private void HandleMessage(JObject obj)
        {
            String uri = obj["uri"].ToObject<string>();
        }
    }
}