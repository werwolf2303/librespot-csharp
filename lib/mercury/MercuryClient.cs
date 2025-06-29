using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using lib.common;
using lib.core;
using lib.json;
using log4net;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using Spotify;
using Packet = lib.crypto.Packet;

namespace lib.mercury
{
    public class MercuryClient : IPacketsReceiver, IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(MercuryClient));
        private static int MERCURY_REQUEST_TIMEOUT = 3000;
        private int _seqHolder = 1;
        private Object _seqHolderLock = new Object();
        private Dictionary<long, ICallback> _callbacks = new Dictionary<long, ICallback>();
        private Object _removeCallbackLock = new Object();
        private List<InternalSubListener> _subscriptions = new List<InternalSubListener>();
        private Object _subscriptionsLock = new Object();
        private Dictionary<long, BytesArrayList> _partials = new Dictionary<long, BytesArrayList>();
        private Session _session;

        public MercuryClient(Session session)
        {
            _session = session;
        }

        public void Subscribe(String uri, ISubListener listener)
        {
            Response response = SendSync(RawMercuryRequest.Sub(uri));
            if (response.StatusCode != 200) throw new PubSubException(response);

            if (response.Payload.Size() > 0)
            {
                while (response.Payload.HasNext())
                {
                    Subscription sub = Serializer.Deserialize<Subscription>(new MemoryStream(response.Payload.Next()));
                    _subscriptions.Add(new InternalSubListener(sub.Uri, listener, true));
                }
            }
            else
            {
                _subscriptions.Add(new InternalSubListener(uri, listener, true));
            }
            
            LOGGER.DebugFormat("Subscribed successfully to {0}!", uri);
        }

        public void Unsubscribe(String uri)
        {
            Response response = SendSync(RawMercuryRequest.Unsub(uri));
            if (response.StatusCode != 200) throw new PubSubException(response);

            int toBeRemoved = _subscriptions.FindIndex(x => x.Matches(uri));
            _subscriptions.RemoveAt(toBeRemoved);
            
            LOGGER.DebugFormat("Unsubscribed successfully from {0}!", uri);
        }
        
        public Response SendSync(RawMercuryRequest request)
        {
            SyncCallback callback = new SyncCallback();
            int seq = Send(request, response => callback.Response(response));

            Response resp = callback.WaitResponse();
            if (resp == null) throw new IOException(String.Format("Request timeout, {0} passed, yet no response. (seq: {1})", MERCURY_REQUEST_TIMEOUT, seq));

            return resp;
        }

        public W SendSync<W>(JsonMercuryRequest<W> request) where W : JsonWrapper
        {
            Response resp = SendSync(request.Request);
            if (resp.StatusCode >= 200 && resp.StatusCode < 300) return request.Instantiate(resp);
            throw new MercuryException(resp);
        }

        public void Send<W>(JsonMercuryRequest<W> request, IJsonCallback<W> callback) where W : JsonWrapper
        {
            try
            {
                Send(request.Request, resp =>
                {
                    if (resp.StatusCode >= 200 && resp.StatusCode < 300) callback.Response(request.Instantiate(resp));
                    else callback.Exception(new MercuryException(resp));
                });
            }
            catch (IOException ex)
            {
                callback.Exception(ex);
            }
        }

        public void Send<P>(ProtobufMercuryRequest request, IProtoCallback<P> callback) where P : IExtensible
        {
            try {
                Send(request.Request, resp => {
                    if (resp.StatusCode >= 200 && resp.StatusCode < 300) {
                        callback.Response(new ProtoWrapperResponse<P>(Serializer.Deserialize<P>(resp.Payload.Stream())));
                    } else {
                        callback.Exception(new MercuryException(resp));
                    }
                });
            } catch (IOException ex) {
                callback.Exception(ex);
            }
        }

        public int Send(RawMercuryRequest request, Callback callback)
        {
            MemoryStream bytesOut = new MemoryStream();
            BinaryWriter outWriter = new BinaryWriter(bytesOut);

            int seq;
            lock (_seqHolderLock)
            {
                seq = _seqHolder;
                _seqHolder++;
            }
            
            LOGGER.DebugFormat("Send Mercury requst, seq: {0}, uri: {1}, method: {2}", seq, request._header.Uri, request._header.Method);
            
            outWriter.WriteBigEndian((short) 4); // Seq length
            outWriter.WriteBigEndian(seq); // Seq
            
            outWriter.Write((byte) 1); // Flags
            outWriter.WriteBigEndian(1 + request._payload.Length); // Parts count
            
            MemoryStream headerStream = new MemoryStream();
            Serializer.Serialize(headerStream, request._header);
            headerStream.Position = 0;
            byte[] headerBytes = headerStream.ToArray();
            outWriter.WriteBigEndian((short) headerBytes.Length); // Header length
            outWriter.Write(headerBytes); // Header
            
            foreach (byte[] part in request._payload)
            {   // Parts
                outWriter.WriteBigEndian((short) part.Length); 
                outWriter.Write(part);
            }
            
            Packet.Type cmd = Packet.ForMethod(request._header.Method);
            _session.Send(cmd, bytesOut.ToArray());
            
            _callbacks.Add(seq, new CallbackWrapper(callback));
            return seq;
        }

        public void Dispatch(Packet packet)
        {
            BinaryReader payload = new BinaryReader(new MemoryStream(packet._payload));
            int seqLength = payload.ReadInt16();
            long seq;
            if (seqLength == 2) seq = payload.ReadInt16();
            else if (seqLength == 4) seq = payload.ReadInt32();
            else if (seqLength == 8) seq = payload.ReadInt64();
            else throw new InvalidOperationException("Unknown seq length: " + seqLength);

            byte flags = payload.ReadByte();
            short parts = payload.ReadInt16();

            BytesArrayList partial = _partials[seq];
            if (partial == null || flags == 0)
            {
                partial = new BytesArrayList();
                _partials.Add(seq, partial);
            }
            
            LOGGER.DebugFormat("Handling packet, cmd: {0}, seq: {1}, flags: {2}, parts: {3}", packet.GetType(), seq, flags, parts);

            for (int i = 0; i < parts; i++)
            {
                short size = payload.ReadInt16();
                byte[] buffer = new byte[size];
                payload.Read(buffer, 0, buffer.Length);
                partial.Add(buffer);
            }

            if (flags != 1) return;

            Header header = Serializer.Deserialize<Header>(new MemoryStream(partial.Get(0)));

            Response resp = new Response(header, partial);

            if (packet.Is(Packet.Type.MercuryEvent))
            {
                bool dispatched = false;
                lock (_subscriptionsLock)
                {
                    foreach (InternalSubListener sub in _subscriptions)
                    {
                        if (sub.Matches(header.Uri))
                        {
                            sub.Dispatch(resp);
                            dispatched = true;
                        }
                    }
                }
                
                if (!dispatched) 
                    LOGGER.DebugFormat("Couldn't dispatch Mercury event (seq: {0}, uri: {1}, code: {2}, payload: {3})", seq, header.Uri, header.StatusCode, resp.Payload.ToHex());
            }else if (packet.Is(Packet.Type.MercuryReq) || packet.Is(Packet.Type.MercurySub) ||
                      packet.Is(Packet.Type.MercuryUnsub))
            {
                ICallback callback = _callbacks[seq];
                _callbacks.Remove(seq);
                if (callback != null)
                    callback.Response(resp);
                else LOGGER.WarnFormat("Skipped Mercury response, seq: {0}, uri: {1}, code: {2}", seq, header.Uri, header.StatusCode);

                lock (_removeCallbackLock)
                {
                    Monitor.PulseAll(_removeCallbackLock);
                }
            }
            else
            {
                LOGGER.WarnFormat("Couldn't handle packet, seq: {0}, uri: {1}, code: {2}", seq, header.Uri, header.StatusCode);
            }
        }

        public void InterestedIn(ISubListener listener, String uri)
        {
            _subscriptions.Add(new InternalSubListener(uri, listener, false));
        }

        public void NotInterested(ISubListener listener)
        {
            int toBeRemoved = 0;
            _subscriptions.ForEach(subListener =>
            {
                if (subListener._listener == listener)
                {
                    toBeRemoved = _subscriptions.IndexOf(subListener);
                }
            });
            _subscriptions.RemoveAt(toBeRemoved);
        }
        
        public void Dispose() {
            if (_subscriptions.Count != 0) {
                foreach (InternalSubListener listener in new List<InternalSubListener>(_subscriptions)) {
                    try {
                        if (listener._isSub) Unsubscribe(listener._uri);
                        else NotInterested(listener._listener);
                    } catch (Exception ex) {
                        if (ex is IOException || ex is MercuryException)
                        {
                            LOGGER.Debug("Failed unsubscribing.", ex);
                        }
                        else throw;
                    }
                }
            }

            if (_callbacks.Count != 0) {
                lock (_removeCallbackLock) {
                    Monitor.Wait(_removeCallbackLock);
                }
            }

            _callbacks = new Dictionary<long, ICallback>();
        }
        
        private class CallbackWrapper : ICallback
        {
            public Callback Callback;

            public CallbackWrapper(Callback callback)
            {
                Callback = callback;
            }

            public void Response(Response response)
            {
                Callback(response);
            }
        }
        
        public delegate void Callback(Response response);
        
        public interface ICallback {
            void Response(Response response);
        }
        
        public interface IJsonCallback<W> where W : JsonWrapper {
            void Response(W json);

            void Exception(Exception ex);
        }

        public interface IProtoCallback<M> where M : ProtoBuf.IExtensible {
            void Response(ProtoWrapperResponse<M> proto);

            void Exception(Exception ex);
        }

        private class SyncCallback : ICallback
        {
            private Response _reference;
            private Object _referenceLock = new Object();


            public void Response(Response response)
            {
                lock (_referenceLock)
                {
                    _reference = response;
                    Monitor.PulseAll(_referenceLock);
                }
            }

            public Response WaitResponse()
            {
                lock (_referenceLock)
                {
                    Monitor.Wait(_referenceLock);
                    return _reference;
                }
            }
        }

        public class ProtoWrapperResponse<P> where P : ProtoBuf.IExtensible
        {
            private P _proto;
            private JObject _json;

            public ProtoWrapperResponse(P proto)
            {
                _proto = proto;
            }

            public P Proto()
            {
                return _proto;
            }

            public JObject Json()
            {
                if (_json == null) _json = JObject.FromObject(_proto);
                return _json;
            }
        }

        public class PubSubException : MercuryException
        {
            public PubSubException(Response response) : base(response)
            {
            }
        }
        
        private class InternalSubListener {
            public String _uri;
            public ISubListener _listener;
            public bool _isSub;

            public InternalSubListener(String uri, ISubListener listener, bool isSub) {
                _uri = uri;
                _listener = listener;
                _isSub = isSub;
            }

            public bool Matches(String uri) {
                return uri.StartsWith(uri);
            }

            public void Dispatch(Response resp) {
                _listener.Event(resp);
            }
        }

        public class MercuryException : Exception
        {
            public int Code;

            public MercuryException(Response response): base("status: " + response.StatusCode)
            {
                Code = response.StatusCode;
            }
        }

        public class Response
        {
            public String Uri;
            public BytesArrayList Payload;
            public int StatusCode;

            public Response(Header header, BytesArrayList payload)
            {
                Uri = header.Uri;
                StatusCode = header.StatusCode;
                Payload = payload.CopyOfRange(1, payload.Size());
            }
        }
    }
}