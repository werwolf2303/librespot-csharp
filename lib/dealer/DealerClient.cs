using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using lib.common;
using lib.core;
using lib.mercury;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;
using spotify.clienttoken.http.v0;
using WebSocketSharp;
using ErrorEventArgs = WebSocketSharp.ErrorEventArgs;

namespace lib.dealer
{
    public class DealerClient : IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(DealerClient));
        private AsyncWorker<Runnable.Run> _asyncWorker;
        private Session _session;
        private Dictionary<String, RequestListener> _reqListeners = new Dictionary<string, RequestListener>();
        private Dictionary<MessageListener, List<String>> _msgListeners =
            new Dictionary<MessageListener, List<string>>();
        private ScheduledExecutorService _scheduler = new ScheduledExecutorService();
        private volatile ConnectionHolder _conn;
        private ScheduledExecutorService.ScheduledFuture<int> _lastScheduledReconnection;

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
                _conn = new ConnectionHolder(this,
                    String.Format("wss://{0}/?access_token={1}", _session.GetAPResolver().getRandomDealer(),
                        _session.GetTokens().Get("playlist-read")));
            }
        }
        
        internal void WaitForListeners() {
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
            
            Dictionary<String, String> headers = GetHeaders(obj);
            JArray payloads = obj["payloads"].ToObject<JArray>();
            byte[] decodedPayload;
            if (payloads != null)
            {
                if ("application/json".Equals(headers["Content-Type"]))
                {
                    if (payloads.Count > 1) throw new Exception("Unsupported");
                    decodedPayload = Encoding.UTF8.GetBytes(payloads[0].ToObject<string>());
                } else if ("text/plain".Equals(headers["Content-Type"]))
                {
                    if (payloads.Count > 1) throw new Exception("Unsupported");
                    decodedPayload = Encoding.UTF8.GetBytes(payloads[0].ToObject<string>());
                }
                else
                {
                    String[] payloadsStr = new String[payloads.Count];
                    for (int i = 0; i < payloads.Count; i++) payloadsStr[i] = payloads[i].ToObject<string>();

                    Stream stream = BytesArrayList.StreamBase64(payloadsStr);
                    if ("gzip".Equals(headers["Transfer-Encoding"]))
                    {
                        try
                        {
                            stream = new GZipStream(stream, CompressionMode.Decompress);
                        }
                        catch (IOException ex)
                        {
                            LOGGER.WarnFormat("Failed decompressing message! (uri: {0})", uri);
                            LOGGER.Error(ex);
                            return;
                        }
                    }

                    MemoryStream writerStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(writerStream);
                    byte[] buffer = new byte[1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) != 0) writer.Write(buffer, 0, read);
                    decodedPayload = writerStream.ToArray();
                    stream.Close();
                }
            }
            else
            {
                decodedPayload = new byte[0];
            }
            
            bool interesting = false;
            lock (_msgListeners)
            {
                foreach (MessageListener listener in _msgListeners.Keys)
                {
                    bool dispatched = false;
                    List<String> keys = _msgListeners[listener];
                    foreach (String key in keys)
                    {
                        if (uri.StartsWith(key) && !dispatched)
                        {
                            interesting = true;
                            _asyncWorker.Submit(() =>
                            {
                                try {
                                    listener.OnMessage(uri, headers, decodedPayload);
                                } catch (IOException ex) {
                                    LOGGER.Error(String.Format("Failed dispatching message! {uri: {}}", uri), ex);
                                } catch (Exception ex) {
                                    LOGGER.Error(String.Format("Failed handling message! {uri: {}}", uri), ex);
                                }
                            });
                            dispatched = true;
                        }
                    }
                }
            }
            
            if(!interesting) LOGGER.Debug("Couldn't dispatch message: " + uri);
        }

        public void AddMessageListener(MessageListener listener, params String[] uris)
        {
            lock (_msgListeners)
            {
                if (_msgListeners.ContainsKey(listener))
                    throw new Exception("A listener for " + Arrays.ToString(uris) + " has already been added.");
                
                _msgListeners.Add(listener, uris.ToList());
                Monitor.PulseAll(_msgListeners);
            }
        }

        public void RemoveMessageListener(MessageListener listener)
        {
            lock (_msgListeners)
            {
                _msgListeners.Remove(listener);
            }
        }

        public void AddRequestListener(RequestListener listener, String uri)
        {
            lock (_msgListeners)
            {
                if (_reqListeners.ContainsKey(uri))
                    throw new Exception("A listener for '" + uri + "' has already been added.");
                
                _reqListeners.Add(uri, listener);
                Monitor.PulseAll(_reqListeners);
            }
        }

        public void RemoveRequestListener(RequestListener listener)
        {
            lock (_reqListeners)
            {
                foreach (String key in _reqListeners.Keys)
                {
                    if (_reqListeners[key] == listener)
                    {
                        _reqListeners.Remove(key);
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            _asyncWorker.Dispose();
            _scheduler.Dispose();

            if (_conn != null)
            {
                ConnectionHolder tmp = _conn;
                _conn = null;
                tmp.Dispose();
            }

            if (_lastScheduledReconnection != null)
            {
                _lastScheduledReconnection.Cancel(true);
                _lastScheduledReconnection = null;
            }
            
            _msgListeners.Clear();
        }

        private void ConnectionInvalidated()
        {
            lock (this)
            {
                if (_lastScheduledReconnection != null && !_lastScheduledReconnection.WasExecuted())
                    throw new Exception("Illegal state!");

                _conn = null;
                
                LOGGER.Debug("Scheduled reconnection attempt in 10 seconds...");
                _lastScheduledReconnection = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    _lastScheduledReconnection = null;

                    try
                    {
                        Connect();
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is MercuryClient.MercuryException)
                        {
                            LOGGER.Error("Failed reconnecting, retrying...", ex);
                            ConnectionInvalidated();
                        }
                        else throw;
                    }
                    return 0;
                }, 10);
            }
        }

        public enum RequestResult
        {
            UnknownSendCommandResult, Success, DeviceNotFound, ContextPlayerError,
            DeviceDisappeared, UpstreamError, DeviceDoesNotSupportCommand, RateLimited
        }

        public interface RequestListener
        {
            RequestResult OnRequest(String mid, int pid, String sender, JObject command);
        }

        public interface MessageListener
        {
            void OnMessage(String uri, Dictionary<String, String> headers, byte[] payload);
        }

        private class ConnectionHolder : IDisposable
        {
            private WebSocket _ws;
            internal bool Closed = false;
            internal bool ReceivedPong = false;
            internal ScheduledExecutorService.ScheduledFuture<int> LastScheduledPing;
            private DealerClient _client;

            internal ConnectionHolder(DealerClient client, String url)
            {
                _client = client;
                _ws = new WebSocket(url);
                _ws.Connect();
                new WebSocketListenerImpl(_ws, this, _client);
            }

            internal void SendPing()
            {
                _ws.Send("{\"type\":\"ping\"}");
            }

            internal void SendReply(String key, RequestResult result)
            {
                bool success = result == RequestResult.Success;
                _ws.Send("{\"type\":\"reply\", \"key\": \"" + key + "\", \"payload\": {\"success\": " + success + "}}");
            }

            public void Dispose()
            {
                if (Closed)
                {
                    _ws.Close();
                }
                else
                {
                    Closed = true;
                    _ws.Close(1000, null);
                }

                if (LastScheduledPing != null)
                {
                    LastScheduledPing.Cancel(false);
                    LastScheduledPing = null;
                }

                if (_client._conn == this)
                {
                    _client.ConnectionInvalidated();
                } else LOGGER.Debug(String.Format("Did not dispatch connection invalidated: {0} != {1}", _client._conn, this));
            }
        }

        private class WebSocketListenerImpl
        {
            private ConnectionHolder _holder;
            private DealerClient _client;
            private WebSocket _ws;
            
            internal WebSocketListenerImpl(WebSocket ws, ConnectionHolder holder, DealerClient client)
            {
                _holder = holder;
                _client = client;
                _ws = ws;
                ws.OnOpen += OnOpen;
                ws.OnMessage += OnMessage;
                ws.OnError += OnError;
            }

            private void OnOpen(object sender, EventArgs e)
            {
                if (_holder.Closed || _client._scheduler.IsShutdown)
                {
                    LOGGER.ErrorFormat("I wonder what happened here... Terminating. (closed: {0})", _holder.Closed);
                    return;
                }
                
                LOGGER.DebugFormat("Dealer connected! (host: {0})", _ws.Url);
                _holder.LastScheduledPing = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    _holder.SendPing();
                    _holder.ReceivedPong = false;
                    
                    _client._scheduler.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                    {
                        if (_holder.LastScheduledPing == null || _holder.LastScheduledPing.IsCancelled) return 0;

                        if (!_holder.ReceivedPong)
                        {
                            LOGGER.Warn("Did not receive ping in 3 seconds. Reconnecting...");
                            _holder.Dispose();
                            return 0;
                        }

                        _holder.ReceivedPong = false;
                        return 0;
                    }, 3));
                    
                    return 0;
                }, 30);
                _client._scheduler.scheduleAtFixedRate(_holder.LastScheduledPing);
            }

            private void OnMessage(object sender, MessageEventArgs e)
            {
                JObject obj = JObject.Parse(e.Data);
                
                _client.WaitForListeners();

                MessageType type = MessageType.Parse(obj["type"].ToObject<string>());
                if (type.Equals(MessageType.Message))
                {
                    try {
                        _client.HandleMessage(obj);
                    } catch (Exception ex) {
                        LOGGER.Warn("Failed handling message: " + obj, ex);
                    }
                }else if (type.Equals(MessageType.Request))
                {
                    try {
                        _client.HandleRequest(obj);
                    } catch (Exception ex) {
                        LOGGER.Warn("Failed handling request: " + obj, ex);
                    }
                } else if (type.Equals(MessageType.Pong))
                {
                    _holder.ReceivedPong = true;
                } else if (type.Equals(MessageType.Ping))
                {
                }
                else
                {
                    throw new Exception("Unknown message type for " + type);
                }
            }

            private void OnError(object sender, ErrorEventArgs e)
            {
                if (_holder.Closed) return;
                
                LOGGER.Warn("An exception occurred. Reconnecting...", e.Exception);
                _holder.Dispose();
            }
        }
    }
}