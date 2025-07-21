using System;
using System.IO;
using System.Net;
using deps.HttpSharp;
using lib.audio.decrypt;
using lib.audio.format;
using lib.audio.storage;
using lib.cache;
using lib.common;
using lib.core;
using lib.dealer;
using lib.mercury;
using log4net;
using ProtoBuf;
using spotify.download.proto;
using spotify.metadata.proto;
using AudioFile = spotify.metadata.proto.AudioFile;

namespace lib.audio.cdn
{
    public class CdnManager
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(CdnManager));
        private Session _session;

        public CdnManager(Session session)
        {
            _session = session;
        }

        private MemoryStream GetHead(byte[] fileId)
        {
            HttpResponse resp = _session.GetClient().NewCall(new HttpRequest(
                _session.GetUserAttribute("head-files-url", "https://heads-fa.spotify.com/head/{file_id}".Replace("{file_id}", Utils.bytesToHex(fileId).ToLower())),
                HttpMethod.Get
            ));

            if (resp.StatusCode != HttpStatusCode.OK)
                throw new IOException((int)resp.StatusCode + ": " + resp.GetResponseString());

            if (resp.GetResponseStream() == null) throw new IOException("Response body is empty!");
            
            MemoryStream responseStream = new MemoryStream();
            resp.GetResponseStream().CopyTo(responseStream);
            
            return responseStream;
        }

        public Streamer StreamExternalEpisode(Episode episode, Uri externalUrl, IHaltListener haltListener)
        {
            return new Streamer(_session, new StreamId(episode), SuperAudioFormat.MP3,
                new CdnUrl(null, externalUrl, this), _session.GetCache(),
                new NoopAudioDecrypt(), haltListener);
        }

        public Streamer StreamFile(AudioFile file, byte[] key, Uri url, IHaltListener listener)
        {
            return new Streamer(_session, new StreamId(file), SuperAudioFormat.get(file.format), new CdnUrl(file.FileId, url, this),
                _session.GetCache(), new AesAudioDecrypt(key), listener);
        }

        private Uri GetAudioUrl(byte[] fileId)
        {
            HttpResponse resp = _session.GetApi().Send(ApiClient.RequestMethod.GET,
                String.Format("/storage-resolve/files/audio/interactive/{0}", Utils.bytesToHex(fileId)));

            if (resp.StatusCode != HttpStatusCode.OK)
                throw new IOException((int)resp.StatusCode + ": " + resp.GetResponseString());
            
            if (resp.GetResponseStream() == null) throw new IOException("Response body is empty!");
            MemoryStream responseStream = new MemoryStream();
            resp.GetResponseStream().CopyTo(responseStream);
            
            StorageResolveResponse proto = Serializer.Deserialize<StorageResolveResponse>(responseStream);
            if (proto.result == StorageResolveResponse.Result.Cdn)
            {
                String url = proto.Cdnurls[new Random().Next(proto.Cdnurls.Count)];
                LOGGER.DebugFormat("Fetched CDN url for {0}: {1}", Utils.bytesToHex(fileId), url);
                return new Uri(url);
            }
            else
            {
                throw new CdnException(String.Format("Could not retrieve CDN url! (result: {0})", proto.result));
            }
        }

        public class CdnException : Exception
        {
            internal CdnException(String message) : base(message)
            {
            }

            internal CdnException(Exception ex) : base("", ex)
            {
            }
        }

        public class InternalResponse
        {
            internal byte[] _buffer;
            internal Headers _headers;

            internal InternalResponse(byte[] buffer, Headers headers)
            {
                _buffer = buffer;
                _headers = headers;
            }
        }

        internal class CdnUrl
        {
            private byte[] _fileId;
            private long _expiration;
            private Uri _url;
            private CdnManager _manager;

            internal CdnUrl(byte[] fileId, Uri url, CdnManager manager)
            {
                _fileId = fileId;
                SetUrl(url);
            }
            
            internal Uri Url()
            {
                if (_expiration == -1) return _url;

                //                                                        5 Minutes
                if (_expiration <= Utils.getUnixTimeStampInMilliseconds() + 300000)
                {
                    try
                    {
                        _url = _manager.GetAudioUrl(_fileId);
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is MercuryClient.MercuryException)
                        {
                            throw new CdnException(ex);
                        } else throw;
                    }
                }

                return _url;
            }

            internal void SetUrl(Uri url)
            {
                _url = url;

                if (_fileId != null)
                {
                    var queryDictionary = System.Web.HttpUtility.ParseQueryString(_url.Query);
                    String tokenStr = queryDictionary["__token__"];
                    if (tokenStr != null && !tokenStr.Equals(""))
                    {
                        long expireAt = -69;
                        String[] split = tokenStr.Split('~');
                        foreach (String str in split)
                        {
                            int i = str.IndexOf('=');
                            if (i == -1) continue;

                            if (str.Substring(0, i).Equals("exp"))
                            {
                                expireAt = long.Parse(str.Substring(i + 1));
                                break;
                            }
                        }

                        if (expireAt == -69)
                        {
                            _expiration = -1;
                            LOGGER.Warn("Invalid __token__ in CDN url: " + _url);
                            return;
                        }

                        _expiration = expireAt * 1000;
                    }
                    else
                    {
                        String param = queryDictionary.Keys[0];
                        int i = param.IndexOf('_');
                        if (i == -1)
                        {
                            _expiration = -1;
                            LOGGER.Warn("Couldn't extract expiration, invalid parameter in CDN url: " + url);
                            return;
                        }

                        _expiration = long.Parse(param.Substring(0, i)) * 1000;
                    }
                }
                else
                {
                    _expiration = -1;
                }
            }
        }

        public class Streamer : IDecodedAudioStream, IGeneralWriteableStream
        {
            private StreamId _streamId;
            private ScheduledExecutorService _executorService;
            private SuperAudioFormat _format;
            private AudioDecrypt _audioDecrypt;
            private CdnUrl _cdnUrl;
            private int _size;
            private byte[][] _buffer;
            private bool[] _available;
            private bool[] _requested;
            private int _chunks;
            private InternalStream _internalStream;
            private CacheManager.Handler _cacheHandler;
            private IHaltListener _haltListener;
            private Session _session;

            internal Streamer(Session session, StreamId streamId, SuperAudioFormat format, CdnUrl cdnUrl, CacheManager cache,
                AudioDecrypt audioDecrypt,
                IHaltListener haltListener)
            {
                _session = session;
                _executorService = _session.GetScheduledExecutorService();
                _streamId = streamId;
                _format = format;
                _audioDecrypt = audioDecrypt;
                _cdnUrl = cdnUrl;
                _haltListener = haltListener;
                _cacheHandler = cache != null ? cache.GetHandler(streamId) : null;

                bool fromCache;
                byte[] firstChunk;
                byte[] sizeHeader;
                if (_cacheHandler != null && (sizeHeader = _cacheHandler.GetHeader(AudioFileFetch.HEADER_SIZE)) != null)
                {
                    _size = new BinaryReader(new MemoryStream(sizeHeader)).ReadInt32() * 4;
                    _chunks = (_size + ChannelManager.CHUNK_SIZE - 1) / ChannelManager.CHUNK_SIZE;

                    try
                    {
                        firstChunk = _cacheHandler.ReadChunk(0);
                        fromCache = true;
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is CacheManager.BadChunkHashException)
                        {
                            LOGGER.Error("Failed getting first chunk from cache.", ex);

                            InternalResponse resp = Request(0, ChannelManager.CHUNK_SIZE - 1);
                            firstChunk = resp._buffer;
                            fromCache = false;
                        }else throw;
                    }
                }
                else
                {
                    InternalResponse resp = Request(0, ChannelManager.CHUNK_SIZE - 1);
                    String contentRange = resp._headers["Content-Range"];
                    if (contentRange == null)
                        throw new IOException("Missing Content-Range header!");
                    
                    String[] split = Utils.split(resp._headers["Content-Range"], '/');
                    _size = Int32.Parse(split[1]);
                    _chunks = (_size + ChannelManager.CHUNK_SIZE - 1) / ChannelManager.CHUNK_SIZE;
                    
                    if (_cacheHandler != null)
                        _cacheHandler.SetHeader(AudioFileFetch.HEADER_SIZE, BitConverter.GetBytes(_size / 4));

                    firstChunk = resp._buffer;
                    fromCache = false;
                }

                _available = new bool[_chunks];
                _requested = new bool[_chunks];
                _buffer = new byte[_chunks][];
                _internalStream = new InternalStream(_session.GetConfiguration().RetryOnChunkError, this);
                _internalStream.Initialize();

                _requested[0] = true;
                WriteChunk(firstChunk, 0, fromCache);
            }

            public void WriteChunk(byte[] chunk, int chunkIndex, bool cached)
            {
                if (_internalStream.IsClosed()) return;

                if (!cached && _cacheHandler != null)
                {
                    try
                    {
                        _cacheHandler.WriteChunk(chunk, chunkIndex);
                    }
                    catch (IOException ex)
                    {
                        LOGGER.WarnFormat("Failed writing to cache! (index: {0})", chunkIndex);
                        LOGGER.Warn("", ex);
                    }
                }

                LOGGER.DebugFormat("Chunk {0}/{1} completed, cached: {2}, stream: {3}", chunkIndex, _chunks, cached,
                    Describe());

                _buffer[chunkIndex] = chunk;
                _audioDecrypt.decryptChunk(chunkIndex, chunk);
                _internalStream.NotifyChunkAvailable(chunkIndex);
            }

            public AbsChunkedInputStream Stream()
            {
                return _internalStream;
            }

            public SuperAudioFormat Codec()
            {
                return _format;
            }

            public String Describe()
            {
                if (_streamId.isEpisode()) return "(episodeGid: " + _streamId.getEpisodeGid() + ")";
                return "(fileId: " + _streamId.getFileId() + ")";
            }

            public int DecryptTimeMS()
            {
                return _audioDecrypt.decryptTimeMs();
            }

            private void RequestChunk(int index)
            {
                if (_cacheHandler != null)
                {
                    try
                    {
                        if (_cacheHandler.HasChunk(index))
                        {
                            _cacheHandler.ReadChunk(index, this);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is IOException || ex is CacheManager.BadChunkHashException)
                        {
                            LOGGER.Error("Failed requesting chunk from cache, index: " + index, ex);
                        }
                        else throw;
                    }
                }

                try
                {
                    InternalResponse resp = Request(index);
                    WriteChunk(resp._buffer, index, false);
                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is CdnException)
                    {
                        LOGGER.Error("Failed requesting chunk from network, index: " + index, ex);
                        _internalStream.NotifyChunkError(index, new AbsChunkedInputStream.ChunkException(ex.Message));
                    }
                }
            }

            public InternalResponse Request(int chunk)
            {
                lock (this)
                {
                    return Request(ChannelManager.CHUNK_SIZE * chunk, (chunk + 1) * ChannelManager.CHUNK_SIZE - 1);
                }
            }

            public InternalResponse Request(int rangeStart, int rangeEnd)
            {
                HttpRequest request = new HttpRequest(_cdnUrl.Url(), HttpMethod.Get);
         
                request.AddRange(rangeStart, rangeEnd);

                HttpResponse response = _session.GetClient().NewCall(request);
                if (response.StatusCode != HttpStatusCode.PartialContent)
                    throw new IOException(response.StatusCode.ToString() + ": " + response.StatusDescription);

                if (response.GetResponseStream() == null)
                    throw new IOException("Response body is emptry!");
                
                MemoryStream responseStream = new MemoryStream();
                response.GetResponseStream().CopyTo(responseStream);

                Headers.Builder headers = new Headers.Builder();
                
                foreach (String key in response.Headers.Keys)
                    headers.Add(key, response.Headers[key]);
                
                return new InternalResponse(responseStream.ToArray(), headers.Build());
            }

            public int Size()
            {
                return _size;
            }

            internal class InternalStream : AbsChunkedInputStream
            {
                private Streamer _streamer;

                internal InternalStream(bool retryOnChunkError, Streamer streamer) : base(retryOnChunkError)
                {
                    _streamer = streamer;
                }

                protected override void Dispose(bool disposing)
                {
                    base.Dispose(disposing);

                    if (_streamer._cacheHandler != null)
                    {
                        try
                        {
                            _streamer._cacheHandler.Dispose();
                        }
                        catch (IOException ignored)
                        {
                        }
                    }
                }

                public override void Flush()
                {
                }

                public override void SetLength(long value)
                {
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                }

                public override bool CanRead
                {
                    get => true;
                }
                public override bool CanSeek
                {
                    get => true;
                }
                public override bool CanWrite
                {
                    get => false;
                }

                protected override byte[][] Buffer()
                {
                    return _streamer._buffer;
                }

                public override int Size()
                {
                    return _streamer._size;
                }

                protected override bool[] RequestedChunks()
                {
                    return _streamer._requested;
                }

                protected override bool[] AvailableChunks()
                {
                    return _streamer._available;
                }

                protected override int Chunks()
                {
                    return _streamer._chunks;
                }

                protected override void RequestChunkFromStream(int index)
                {
                    _streamer._executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(() =>
                    {
                        _streamer.RequestChunk(index);
                        return 0;
                    }, 0));
                }

                public void StreamReadHalted(int chunk, long time)
                {
                    if (_streamer._haltListener != null) _streamer._executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(
                        () =>
                        {
                            _streamer._haltListener.StreamReadHalted(chunk, time);
                            return 0;
                        }, 0));
                }

                public void StreamReadResumed(int chunk, long time)
                {
                    if (_streamer._haltListener != null) _streamer._executorService.schedule(new ScheduledExecutorService.ScheduledFuture<int>(
                        () =>
                        {
                            _streamer._haltListener.StreamReadResumed(chunk, time);
                            return 0;
                        }, 0));
                }
            }
        }
    }
}