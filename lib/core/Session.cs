using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using Connectstate;
using deps.HttpSharp;
using lib.audio;
using lib.audio.cdn;
using lib.audio.storage;
using lib.cache;
using lib.common;
using lib.crypto;
using lib.dealer;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;
using ProtoBuf;
using Spotify;
using spotify.explicitcontent.proto;
using ClientHello = Spotify.ClientHello;

namespace lib.core
{
    public class Session : IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(Session));

        private static byte[] _serverKey =
        {
            0xac, 0xe0, 0x46, 0x0b, 0xff, 0xc2, 0x30, 0xaf, 0xf4, 0x6b, 0xfe, 0xc3,
            0xbf, 0xbf, 0x86, 0x3d, 0xa1, 0x91, 0xc6, 0xcc, 0x33, 0x6c, 0x93, 0xa1,
            0x4f, 0xb3, 0xb0, 0x16, 0x12, 0xac, 0xac, 0x6a, 0xf1, 0x80, 0xe7, 0xf6,
            0x14, 0xd9, 0x42, 0x9d, 0xbe, 0x2e, 0x34, 0x66, 0x43, 0xe3, 0x62, 0xd2,
            0x32, 0x7a, 0x1a, 0x0d, 0x92, 0x3b, 0xae, 0xdd, 0x14, 0x02, 0xb1, 0x81,
            0x55, 0x05, 0x61, 0x04, 0xd5, 0x2c, 0x96, 0xa4, 0x4c, 0x1e, 0xcc, 0x02,
            0x4a, 0xd4, 0xb2, 0x0c, 0x00, 0x1f, 0x17, 0xed, 0xc2, 0x2f, 0xc4, 0x35,
            0x21, 0xc8, 0xf0, 0xcb, 0xae, 0xd2, 0xad, 0xd7, 0x2b, 0x0f, 0x9d, 0xb3,
            0xc5, 0x32, 0x1a, 0x2a, 0xfe, 0x59, 0xf3, 0x5a, 0x0d, 0xac, 0x68, 0xf1,
            0xfa, 0x62, 0x1e, 0xfb, 0x2c, 0x8d, 0x0c, 0xb7, 0x39, 0x2d, 0x92, 0x47,
            0xe3, 0xd7, 0x35, 0x1a, 0x6d, 0xbd, 0x24, 0xc2, 0xae, 0x25, 0x5b, 0x88,
            0xff, 0xab, 0x73, 0x29, 0x8a, 0x0b, 0xcc, 0xcd, 0x0c, 0x58, 0x67, 0x31,
            0x89, 0xe8, 0xbd, 0x34, 0x80, 0x78, 0x4a, 0x5f, 0xc9, 0x6b, 0x89, 0x9d,
            0x95, 0x6b, 0xfc, 0x86, 0xd7, 0x4f, 0x33, 0xa6, 0x78, 0x17, 0x96, 0xc9,
            0xc3, 0x2d, 0x0d, 0x32, 0xa5, 0xab, 0xcd, 0x05, 0x27, 0xe2, 0xf7, 0x10,
            0xa3, 0x96, 0x13, 0xc4, 0x2f, 0x99, 0xc0, 0x27, 0xbf, 0xed, 0x04, 0x9c,
            0x3c, 0x27, 0x58, 0x04, 0xb6, 0xb2, 0x19, 0xf9, 0xc1, 0x2f, 0x02, 0xe9,
            0x48, 0x63, 0xec, 0xa1, 0xb6, 0x42, 0xa0, 0x9d, 0x48, 0x25, 0xf8, 0xb3,
            0x9d, 0xd0, 0xe8, 0x6a, 0xf9, 0x48, 0x4d, 0xa1, 0xc2, 0xba, 0x86, 0x30,
            0x42, 0xea, 0x9d, 0xb3, 0x08, 0x6c, 0x19, 0x0e, 0x48, 0xb3, 0x9d, 0x66,
            0xeb, 0x00, 0x06, 0xa2, 0x5a, 0xee, 0xa1, 0x1b, 0x13, 0x87, 0x3c, 0xd7,
            0x19, 0xe6, 0x55, 0xbd
        };
        private ApResolver _apResolver;
        private DiffieHellman _keys;
        private Inner _inner;
        private ScheduledExecutorService _scheduler = new ScheduledExecutorService();
        private Object _authLock = new Object();
        private bool _authLockState;
        private HttpClient _client;
        private List<CloseListener> _closeListeners = new List<CloseListener>();
        private List<ReconnectionListener> _reconnectionListeners = new List<ReconnectionListener>();
        private Object _reconnectionListenersLock = new Object();
        private Dictionary<String, String> _userAttributes = new Dictionary<String, String>();
        private ConnectionHolder _conn;
        private volatile CipherPair _cipherPair;
        private Receiver _receiver;
        private APWelcome _apWelcome;
        private MercuryClient _mercuryClient;
        private AudioKeyManager _audioKeyManager;
        private ChannelManager _channelManager;
        private TokenProvider _tokenProvider;
        private CdnManager _cdnManager;
        private CacheManager _cacheManager;
        private DealerClient _dealer;
        private ApiClient _api;
        private SearchManager _search;
        private PlayableContentFeeder _contentFeeder;
        private EventService _eventService;
        private String _countryCode;
        private volatile bool _closed;
        private volatile bool _closing;
        private volatile ScheduledExecutorService.ScheduledFuture<int> _scheduledReconnect;

        private Session(Inner inner)
        {
            _inner = inner;
            _keys = new DiffieHellman(inner.Random);
            _client = CreateClient(inner.Conf);
            _apResolver = new ApResolver(_client);
            String addr = _apResolver.getRandomAccesspoint();
            _conn = ConnectionHolder.Create(addr, inner.Conf);

            _scheduledReconnect = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    LOGGER.Warn("Socket timed out. Reconnecting...");
                    Reconnect();
                    return 0;
                }, 2 * 60 + GetConfiguration().ConnectionTimeout);

            LOGGER.Info(String.Format("Created new session! (deviceId: {0}, ap: {1}, proxy: {2})", inner.DeviceId, addr,
                false));
        }

        private static HttpClient CreateClient(Configuration configuration)
        {
            HttpClient client = new HttpClient();
            client.RetryOnConnectionFailure = true;
            
            client.AddInterceptor(request =>
            {
                request.UserAgent = Version.systemInfoString();
                
                if (request.RequestData == null || request.ExtraHeaders.ContainsKey("Content-Encoding") || request.ContentEncoding != null)
                    return true;

                request.ContentEncoding = "gzip";
                
                using (var output = new MemoryStream())
                {
                    using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                    {
                        gzip.Write(request.RequestData, 0, request.RequestData.Length);
                    }
                    byte[] compressedData = output.ToArray();
                    request.RequestData = compressedData;
                    request.ContentLength = compressedData.Length;
                }

                return true;
            });
            
            client.Proxy = configuration.Proxy;

            return client;
        }

        private static int ReadBlobInt(ByteBuffer buffer)
        {
            int lo = buffer.ReadByte();
            if ((lo & 0x80) == 0) return lo;
            int hi = buffer.ReadByte();
            return lo & 0x7f | hi << 7;
        }
        
        public HttpClient GetClient()
        {
            return _client;
        }

        private void Connect()
        {
            MemoryStream accStream = new MemoryStream();
            BinaryWriter acc = new BinaryWriter(accStream);

            byte[] nonce = new byte[0x10];
            _inner.Random.GetBytes(nonce);

            ClientHello clientHello = new ClientHello
            {
                BuildInfo = Version.standardBuildInfo(),
                CryptosuitesSupporteds = { Cryptosuite.CryptoSuiteShannon },
                LoginCryptoHello = new LoginCryptoHelloUnion
                {
                    DiffieHellman = new LoginCryptoDiffieHellmanHello
                    {
                        Gc = _keys.PublicKeyArray(),
                        ServerKeysKnown = 1
                    }
                },
                ClientNonce = nonce,
                Padding = new byte[] { 0x1e }
            };


            byte[] clientHelloBytes;
            var memStream = new MemoryStream();
            Serializer.Serialize(memStream, clientHello);
            clientHelloBytes = memStream.ToArray();
            
            int length = 2 + 4 + clientHelloBytes.Length;
            _conn.Out.Write((byte)0);
            _conn.Out.Write((byte)4);
            _conn.Out.WriteBigEndian(length); 
            _conn.Out.Write(clientHelloBytes);
            _conn.Out.Flush();
            
            acc.Write((byte)0);
            acc.Write((byte)4);
            acc.WriteBigEndian(length);
            acc.Write(clientHelloBytes);
            
            byte[] apResponseLengthBytes = _conn.In.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(apResponseLengthBytes);
            int apResponseLength = BitConverter.ToInt32(apResponseLengthBytes, 0);
            acc.WriteBigEndian(apResponseLength);

            byte[] apResponseMessageBytes = new byte[apResponseLength - 4];
            _conn.In.ReadFully(apResponseMessageBytes);
            acc.Write(apResponseMessageBytes);
            
            MemoryStream apResponseStream = new MemoryStream();
            apResponseStream.Write(apResponseMessageBytes, 0, apResponseMessageBytes.Length);
            apResponseStream.Position = 0;
            APResponseMessage apResponseMessage = Serializer.Deserialize<APResponseMessage>(apResponseStream);
            byte[] sharedKey = Utils.toByteArray(_keys.ComputeSharedKey(apResponseMessage.Challenge.LoginCryptoChallenge.DiffieHellman.Gs));

            RsaKeyParameters publicKey = new RsaKeyParameters(
                false,
                new BigInteger(1, _serverKey),
                BigInteger.ValueOf(65537));

            ISigner signer = new RsaDigestSigner(new Sha1Digest());
            signer.Init(false, publicKey);
             
            byte[] dataHellmanGs = apResponseMessage.Challenge.LoginCryptoChallenge.DiffieHellman.Gs;
            signer.BlockUpdate(dataHellmanGs, 0, dataHellmanGs.Length);
            
            byte[] signature = apResponseMessage.Challenge.LoginCryptoChallenge.DiffieHellman.GsSignature;
            
            bool verified = signer.VerifySignature(signature);
            if (!verified)
                throw new Exception("Failed signature check!");
            
            MemoryStream data = new MemoryStream(100);
            
            HMac mac = new HMac(new Sha1Digest());
            KeyParameter keyParam = new KeyParameter(sharedKey);
            mac.Init(keyParam);
            
            byte[] accBytes = accStream.ToArray();
            for (int i = 1; i < 6; i++)
            {
                mac.BlockUpdate(accBytes, 0, accBytes.Length); 
                mac.BlockUpdate(new byte[] { (byte)i }, 0, 1);

                byte[] result = new byte[mac.GetMacSize()];
                mac.DoFinal(result, 0);
                data.Write(result, 0, result.Length);
                mac.Reset();
            }

            byte[] dataArray = data.ToArray();
            mac.Init(new KeyParameter(Arrays.CopyOfRange(dataArray, 0, 20)));
            mac.BlockUpdate(accBytes, 0, accBytes.Length);

            byte[] challenge = new byte[mac.GetMacSize()];
            mac.DoFinal(challenge, 0);

            ClientResponsePlaintext clientResponsePlaintext = new ClientResponsePlaintext
            {
                LoginCryptoResponse = new LoginCryptoResponseUnion
                {
                    DiffieHellman = new LoginCryptoDiffieHellmanResponse
                    {
                        Hmac = challenge
                    }
                },
                PowResponse = new PoWResponseUnion(),
                CryptoResponse = new CryptoResponseUnion()
            };
            
            MemoryStream clientResponsePlaintextStream = new MemoryStream();
            Serializer.Serialize(clientResponsePlaintextStream, clientResponsePlaintext);
            
            byte[] clientResponsePlaintextBytes = clientResponsePlaintextStream.ToArray();
            length = 4 + clientResponsePlaintextBytes.Length;
            _conn.Out.WriteBigEndian(length);
            _conn.Out.Write(clientResponsePlaintextBytes);
            _conn.Out.Flush();
            
            try
            {
                byte[] scrap = new byte[4];
                _conn.Stream.ReadTimeout = 300;
                int read = _conn.In.Read(scrap, 0, scrap.Length); 
                if (read == scrap.Length)
                {
                    length = (scrap[0] << 24) | (scrap[1] << 16) | (scrap[2] << 8) | (scrap[3] & 0xFF);
                    byte[] payload = new byte[length - 4];
                    _conn.In.ReadFully(payload);
                    MemoryStream payloadStream = new MemoryStream();
                    payloadStream.Write(payload, 0, payload.Length);
                    payloadStream.Position = 0;
                    APLoginFailed failed = Serializer.Deserialize<APResponseMessage>(payloadStream).LoginFailed;
                    throw new SpotifyAuthenticationException(failed);
                }
                else if (read > 0)
                {
                    throw new Exception("Read unknown data!");
                }
            }
            catch (IOException e) when ((e.InnerException as SocketException)?.SocketErrorCode == SocketError.TimedOut)
            {
            }
            catch (IOException e) when ((e.InnerException as SocketException)?.SocketErrorCode ==
                                        SocketError.WouldBlock)
            {
                //https://github.com/mono/mono/blob/main/mcs/class/System/Test/System.Net.Sockets/NetworkStreamTest.cs#L71-L72
            }
            finally
            {
                _conn.Stream.ReadTimeout = -1;
            }
            lock (_authLock)
            {
                _cipherPair = new CipherPair(
                    Arrays.CopyOfRange(dataArray, 20, 52), 
                    Arrays.CopyOfRange(dataArray, 52, 84)
                );
                _authLockState = true;
            }
            
            LOGGER.Info("Connected successfully!");
        }
        
        private void Authenticate(LoginCredentials credentials)
        {
            AuthenticatePartial(credentials, false);

            if (credentials.GetType() == AuthenticationType.AuthenticationSpotifyToken.GetType())
                Reconnect();

            lock (_authLock)
            {
                _mercuryClient = new MercuryClient(this);
                _tokenProvider = new TokenProvider(this);
                _audioKeyManager = new AudioKeyManager(this);
                _channelManager = new ChannelManager(this);
                _api = new ApiClient(this);
                _cdnManager = new CdnManager(this);
                _contentFeeder = new PlayableContentFeeder(this);
                _cacheManager = new CacheManager(_inner.Conf);
                _dealer = new DealerClient(this);
                _search = new SearchManager(this);
                _eventService = new EventService(this);

                _authLockState = false;
                Monitor.PulseAll(_authLock);
            }
            
            TimeProvider.init(this);
            _dealer.Connect();
            
            
            LOGGER.InfoFormat("Authenticated as {0}!", _apWelcome.CanonicalUsername);
            GetMercury().InterestedIn(new MercuryAttributesUpdate(this), "spotify:user:attributes:update");
            GetDealer().AddMessageListener(new DealerConnectLogout(this), "hm://connect-state/v1/connect/logout");
        }
        
        private class DealerConnectLogout : DealerClient.MessageListener
        {
            private Session _session;

            internal DealerConnectLogout(Session session)
            {
                _session = session;
            }
            
            public void OnMessage(string uri, Dictionary<string, string> headers, byte[] payload)
            {
                if (uri.Equals("hm://connect-state/v1/connect/logout"))
                {
                    try
                    {
                        _session.Dispose();
                    }
                    catch (IOException ex)
                    {
                        LOGGER.Error("Failed closing session due to logout.", ex);
                    }
                }
            }
        }

        private class MercuryAttributesUpdate : ISubListener
        {
            private Session _session;

            internal MercuryAttributesUpdate(Session session)
            {
                _session = session;
            }
            
            public void Event(MercuryClient.Response resp)
            {
                if (resp.Uri.Equals("spotify:user:attributes:update")) {
                    UserAttributesUpdate attributesUpdate;
                    try {
                        attributesUpdate = Serializer.Deserialize<UserAttributesUpdate>(resp.Payload.Stream());
                    } catch (IOException ex) {
                        LOGGER.Warn("Failed parsing user attributes update.", ex);
                        return;
                    }

                    foreach (KeyValuePair pair in attributesUpdate.Pairs) {
                        _session._userAttributes.Add(pair.Key, pair.Value);
                        LOGGER.DebugFormat("Updated user attribute: {0} -> {1}", pair.Key, pair.Value);
                    }
                }
            }
        }

        private void AuthenticatePartial(LoginCredentials credentials, bool removeLock)
        { 
            if (_conn == null || _cipherPair == null) throw new Exception("Illegal state! Connection not established!");
            
            ClientResponseEncrypted clientResponseEncrypted = new ClientResponseEncrypted
            {
                LoginCredentials = credentials,
                SystemInfo = new SystemInfo
                {
                    Os = Os.OsUnknown,
                    CpuFamily = CpuFamily.CpuUnknown,
                    SystemInformationString = Version.systemInfoString(),
                    DeviceId = _inner.DeviceId
                },
                VersionString = Version.versionString()
            };
            
            MemoryStream clientResponseEncryptedStream = new MemoryStream();
            Serializer.Serialize(clientResponseEncryptedStream, clientResponseEncrypted);
            
            SendUnchecked(Packet.Type.Login, clientResponseEncryptedStream.ToArray());
            
            Packet packet = _cipherPair.ReceiveEncoded(_conn.In);
            if (packet.Is(Packet.Type.APWelcome))
            {
                _apWelcome = Serializer.Deserialize<APWelcome>(new MemoryStream(packet._payload));

                _receiver = new Receiver(this);

                byte[] bytes0x0f = new byte[20];
                GetRandom().GetBytes(bytes0x0f);
                SendUnchecked(Packet.Type.Unknown_0x0f, bytes0x0f);

                byte[] preferredLocaleBytes = Encoding.UTF8.GetBytes(_inner.PreferredLocale);
                MemoryStream preferredLocale = new MemoryStream();
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x10);
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x02);

                byte[] preferredLocaleStringBytes = Encoding.UTF8.GetBytes("preferred-locale");
                preferredLocale.Write(preferredLocaleStringBytes, 0, preferredLocaleStringBytes.Length);
                
                preferredLocale.Write(preferredLocaleBytes, 0, preferredLocaleBytes.Length);
                SendUnchecked(Packet.Type.PreferredLocale, preferredLocale.ToArray());

                if (removeLock)
                {
                    lock (_authLock)
                    {
                        _authLockState = false;
                        Monitor.PulseAll(_authLock);
                    }
                }

                if (_inner.Conf.StoreCredentials)
                {
                    byte[] reusable = _apWelcome.ReusableAuthCredentials;
                    AuthenticationType reusableType = _apWelcome.ReusableAuthCredentialsType;
                    
                    JObject obj = new JObject();
                    obj["username"] = _apWelcome.CanonicalUsername;
                    obj["credentials"] = Base64.ToBase64String(reusable);
                    obj["type"] = reusableType.ToString();

                    if (_inner.Conf.StoredCredentialsFile == null) throw new Exception("Illegal argument");
                    FileStream credentialsFileStream = File.OpenWrite(_inner.Conf.StoredCredentialsFile);
                    byte[] credentialsBytes = Encoding.UTF8.GetBytes(obj.ToString());
                    credentialsFileStream.Write(credentialsBytes, 0, credentialsBytes.Length);
                    credentialsFileStream.Close();
                }
            }else if (packet.Is(Packet.Type.AuthFailure))
            {
                throw new SpotifyAuthenticationException(Serializer.Deserialize<APLoginFailed>(new MemoryStream(packet._payload)));
            }
            else
            {
                throw new Exception("Illegal state! Unknown CMD 0x" + Utils.bytesToHex(new[] { packet._cmd }));
            }
        }
        
        public void Dispose()
        {
            LOGGER.Info("Closing session. (deviceId: " + _inner.DeviceId + ")");

            if (_scheduledReconnect != null) _scheduledReconnect.Cancel(true);

            _closing = true;
            
            _scheduler.Dispose();

            if (_dealer != null)
            {
                _dealer.Dispose();
                _dealer = null;
            }

            _audioKeyManager = null;

            if (_channelManager != null) {
                _channelManager.Dispose();
                _channelManager = null;
            }

            if (_eventService != null) {
                _eventService.Dispose();
                _eventService = null;
            }

            if (_mercuryClient != null) {
                _mercuryClient.Dispose();
                _mercuryClient = null;
            }

            if (_receiver != null) {
                _receiver.Stop();
                _receiver = null;
            }

            _client = null;

            if (_conn != null)
            {
                _conn.Socket.Close();
                _conn = null;
            }

            lock (_authLock)
            {
                _apWelcome = null;
                _cipherPair = null;
                _closed = true;
            }

            lock (_closeListeners)
            {
                foreach (var listener in _closeListeners)
                {
                    listener.OnClose();
                }
                
                _closeListeners.Clear();
            }
            
            _reconnectionListeners.Clear();
            
            LOGGER.InfoFormat("Closed session. (deviceId: {0})", _inner.DeviceId);
        }

        private void SendUnchecked(Packet.Type cmd, byte[] payload)
        {
            if (_conn == null) 
                throw new Exception("Illegal state! Cannot write to missing connection.");

            _cipherPair.SendEncoded(_conn.Out, (byte)cmd, payload);
        }

        private void WaitAuthLock()
        {
            if (_closing && _conn == null)
            {
                LOGGER.Debug("Connection was broken while closing.");
                return;
            }

            if (_closed) throw new Exception("Illegal state! Session is closed!");

            lock (_authLock)
            {
                if (_cipherPair == null || _authLockState)
                {
                    Monitor.Wait(_authLock);
                }
            }
        }

        public void Send(Packet.Type cmd, byte[] payload)
        {
            if (_closing && _conn == null)
            {
                LOGGER.Debug("Connection was broken while closing.");
                return;
            }
            
            if (_closed) throw new Exception("Illegal state! Session is closed!");

            lock (_authLock)
            {
                if (_cipherPair == null || _authLockState)
                {
                    Monitor.Wait(_authLock);
                }
                
                SendUnchecked(cmd, payload);
            }
        }

        public ApResolver GetAPResolver()
        {
            return _apResolver;
        }

        public MercuryClient GetMercury()
        {
            WaitAuthLock();
            if (_mercuryClient == null) throw new Exception("Session isn't authenticated!");
            return _mercuryClient;
        }
        
        public AudioKeyManager GetAudioKey()
        {
            WaitAuthLock();
            if (_audioKeyManager == null) throw new Exception("Session isn't authenticated!");
            return _audioKeyManager;
        }

        public CacheManager GetCache()
        {
            WaitAuthLock();
            if (_cacheManager == null) throw new Exception("Session isn't authenticated!");
            return _cacheManager;
        }

        public CdnManager GetCdn()
        {
            WaitAuthLock();
            if (_cdnManager == null) throw new Exception("Session isn't authenticated!");
            return _cdnManager;
        }

        public ChannelManager GetChannel()
        {
            WaitAuthLock();
            if (_channelManager == null) throw new Exception("Session isn't authenticated!");
            return _channelManager;
        }

        public TokenProvider GetTokens()
        {
            WaitAuthLock();
            if (_tokenProvider == null) throw new Exception("Session isn't authenticated!");
            return _tokenProvider;
        }

        public DealerClient GetDealer()
        {
            WaitAuthLock();
            if (_dealer == null) throw new Exception("Session isn't authenticated!");
            return _dealer;
        }

        public ApiClient GetApi()
        {
            WaitAuthLock();
            if (_api == null) throw new Exception("Session isn't authenticated!");
            return _api;
        }

        public PlayableContentFeeder GetContentFeeder()
        {
            WaitAuthLock();
            if (_contentFeeder == null) throw new Exception("Session isn't authenticated!");
            return _contentFeeder;
        }

        public SearchManager GetSearch()
        {
            WaitAuthLock();
            if (_search == null) throw new Exception("Session isn't authenticated!");
            return _search;
        }

        public EventService GetEventService()
        {
            WaitAuthLock();
            if (_eventService == null) throw new Exception("Session isn't authenticated!");
            return _eventService;
        }

        public String Username()
        {
            return _apWelcome.CanonicalUsername;
        }

        public APWelcome GetAPWelcome()
        {
            WaitAuthLock();
            if (_apWelcome == null) throw new Exception("Session isn't authenticated!");
            return _apWelcome;
        }

        public bool IsValid()
        {
            if (_closed) return false;
            
            WaitAuthLock();
            return _apWelcome != null && _conn != null && _conn.Socket.Connected;
        }

        public bool Reconnecting()
        {
            return !_closing && !_closed && _conn == null;
        }

        public String GetCountryCode()
        {
            return _countryCode;
        }
        
        public String GetDeviceId()
        {
            return _inner.DeviceId;
        }

        public String GetPreferredLocale()
        {
            return _inner.PreferredLocale;
        }

        public DeviceType GetDeviceType()
        {
            return _inner.DeviceType;
        }

        public String GetDeviceName()
        {
            return _inner.DeviceName;
        }
        
        public RandomNumberGenerator GetRandom()
        {
            return _inner.Random;
        }
        
        public Configuration GetConfiguration()
        {
            return _inner.Conf;
        }

        public ScheduledExecutorService GetScheduledExecutorService()
        {
            return _scheduler;
        }

        private void Reconnect()
        {
            if (_closing) return;

            lock (_reconnectionListenersLock)
            {
                _reconnectionListeners.ForEach(l => l.OnConnectionDropped());
            }

            try
            {
                if (_conn != null)
                {
                    _receiver.Stop();
                    _conn.Socket.Close();
                }

                _apResolver.refreshPool();

                _conn = ConnectionHolder.Create(_apResolver.getRandomAccesspoint(), _inner.Conf);
                Connect();
                AuthenticatePartial(
                    new LoginCredentials
                    {
                        Username = _apWelcome.CanonicalUsername,
                        Typ = _apWelcome.ReusableAuthCredentialsType,
                        AuthData = _apWelcome.ReusableAuthCredentials
                    }, true
                );
                
                LOGGER.Info("Re-authenticated as " + _apWelcome.CanonicalUsername + "!");

                lock (_reconnectionListenersLock)
                {
                    _reconnectionListeners.ForEach(l => l.OnConnectionEstablished());
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is SpotifyAuthenticationException)
                {
                    _conn = null;
                    LOGGER.Error("Failed reconnecting, retrying in 10 seconds...", ex);

                    _scheduler.schedule(
                        new ScheduledExecutorService.ScheduledFuture<int>(
                            () =>
                            {
                                Reconnect();
                                return 0;
                            }, 10
                        )
                    );
                }
                else throw;
            }
        }

        public void AddCloseListener(CloseListener listener)
        {
            if (!_closeListeners.Contains(listener)) _closeListeners.Add(listener);
        }

        public void AddReconnectionListener(ReconnectionListener listener)
        {
            if (!_reconnectionListeners.Contains(listener)) _reconnectionListeners.Add(listener);
        }

        public void RemoveReconnectionListener(ReconnectionListener listener)
        {
            _reconnectionListeners.Remove(listener);
        }

        private void ParseProductInfo(byte[] payload)
        {
            if (_userAttributes.Count != 0) _userAttributes.Clear();
            
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Encoding.UTF8.GetString(payload));

            if (doc.GetElementsByTagName("product").Count == 0) return;
            XmlNode products = doc.GetElementsByTagName("products")[0];

            if (doc.ChildNodes.Count == 0) return;
            XmlNode product = products.ChildNodes[0];

            XmlNodeList properties = product.ChildNodes;
            
            for (int i = 0; i < properties.Count; i++)
            {
                XmlNode node = properties[i];
                _userAttributes.Add(node.Name, node.InnerText);
            }
            
            String userAttributesDebugString = "{" + string.Join(",", _userAttributes.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}";
            LOGGER.Debug("Parsed product info: " + userAttributesDebugString);
        }

        public String GetUserAttribute(String key)
        {
            return _userAttributes.TryGetValue(key, out var value) ? value : null;
        }

        public String GetUserAttribute(String key, String fallback)
        {
            return _userAttributes.TryGetValue(key, out String value) ? value : fallback;
        }
        
        public interface ReconnectionListener
        {
            void OnConnectionDropped();

            void OnConnectionEstablished();
        }

        public interface CloseListener
        {
            void OnClose();
        }
        
        private class Inner
        {
            public DeviceType DeviceType;
            public String DeviceName;
            public RandomNumberGenerator Random;
            public String DeviceId;
            public Configuration Conf;
            public String PreferredLocale;

            public Inner(DeviceType deviceType, String deviceName, String deviceId, String preferredLocale, Configuration conf)
            {
                Random = RandomNumberGenerator.Create();
                PreferredLocale = preferredLocale;
                Conf = conf;
                DeviceType = deviceType;
                DeviceName = deviceName; 
                DeviceId = String.IsNullOrEmpty(deviceId) ? Utils.randomHexString(Random, 40).ToLower() : deviceId;
            }
        }

        public abstract class AbsBuilder<B> where B : AbsBuilder<B>
        {
            protected Configuration Conf;
            protected String DeviceId = null;
            protected String ClientToken = null;
            protected String DeviceName = "librespot-csharp";
            protected DeviceType DeviceType = DeviceType.Computer;
            protected String PreferredLocale = "en";

            public AbsBuilder(Configuration conf)
            {
                Conf = conf;
            }

            protected AbsBuilder() : this(new Configuration.Builder().Build())
            {
            }

            protected abstract B This();

            /**
             * Sets the preferred locale for the user.
             *
             * @param locale A 2 chars locale code
             */
            public B SetPreferredLocale(String locale)
            {
                if (locale.Length != 2)
                    throw new Exception("Invalid locale: " + locale);

                PreferredLocale = locale;
                return This();
            }

            /**
             * Sets the device name that will appear on Spotify Connect.
             *
             * @param deviceName The device name
             */
            public B SetDeviceName(String deviceName)
            {
                DeviceName = deviceName;
                return This();
            }

            /**
             * Sets the device ID. If not provided or empty will be generated randomly.
             *
             * @param deviceId A 40 chars string
             */
            public B SetDeviceId(String deviceId)
            {
                if (deviceId != null && deviceId.Length != 40)
                    throw new Exception("Device ID must be 40 chars long.");

                DeviceId = deviceId;
                return This();
            }

            /**
             * Sets the client token. If empty, it will be retrieved.
             *
             * @param token A 168 bytes Base64 encoded string
             */
            public B SetClientToken(String token)
            {
                ClientToken = token;
                return This();
            }

            /**
             * Sets the device type.
             *
             * @param deviceType The {@link com.spotify.connectstate.Connect.DeviceType}
             */
            public B SetDeviceType(DeviceType deviceType)
            {
                DeviceType = deviceType;
                return This();
            }
        }

        public class Builder : AbsBuilder<Builder>
        {
            private LoginCredentials _loginCredentials = null;
            private OAuth _oAuth = null;

            public Builder(Configuration conf) : base(conf)
            {
            }

            public Builder()
            {
            }

            protected override Builder This()
            {
                return this;
            }

            private static LoginCredentials DecryptBlob(String deviceId, String username, byte[] encryptedBlob)
            {
                encryptedBlob = Base64.Decode(encryptedBlob);
                
                Sha1Digest sha1 = new Sha1Digest();
                byte[] deviceIdBytes = Encoding.UTF8.GetBytes(deviceId);
                byte[] secret = new byte[sha1.GetDigestSize()];
                sha1.BlockUpdate(deviceIdBytes, 0, deviceIdBytes.Length);
                sha1.DoFinal(secret, 0);

                Pkcs5S2ParametersGenerator gen = new Pkcs5S2ParametersGenerator(new Sha1Digest());
                gen.Init(secret, Encoding.UTF8.GetBytes(username), 0x100);
                byte[] baseKey = ((KeyParameter)gen.GenerateDerivedParameters(160)).GetKey(); 

                sha1 = new Sha1Digest();
                byte[] digest = new byte[sha1.GetDigestSize()];
                sha1.BlockUpdate(baseKey, 0, baseKey.Length);
                sha1.DoFinal(digest, 0);

                byte[] key = new byte[24];
                Array.Copy(digest, 0, key, 0, 20);
                byte[] int20 = BitConverter.GetBytes(20);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(int20);
                Array.Copy(int20, 0, key, 20, 4);

                IBufferedCipher cipher = CipherUtilities.GetCipher("AES/ECB/NoPadding");
                cipher.Init(false, new KeyParameter(key)); 
                byte[] decryptedBlob = cipher.DoFinal(encryptedBlob);

                int l = decryptedBlob.Length;
                for (int i = 0; i < l - 0x10; i++)
                    decryptedBlob[l - i - 1] ^= decryptedBlob[l - i - 0x11];
                
                ByteBuffer blob = ByteBuffer.Wrap(decryptedBlob);
                blob.ReadByte();
                int len = ReadBlobInt(blob);
                byte[] lenBytes = new byte[len];
                blob.Read(lenBytes, 0, lenBytes.Length);
                blob.ReadByte();

                int typeInt = ReadBlobInt(blob);
                AuthenticationType type = AuthenticationType.AuthenticationFacebookToken;
                bool foundType = false;
                foreach (AuthenticationType t in Enum.GetValues(typeof(AuthenticationType)))
                    if ((int)t == typeInt)
                    {
                        type = t;
                        foundType = true;
                        break;
                    }
                if (!foundType)
                    throw new IOException("Unknown AuthenticationType: " + typeInt);

                blob.ReadByte();

                len = ReadBlobInt(blob);
                byte[] authData = new byte[len];
                blob.Read(authData, 0, len);

                return new LoginCredentials
                {
                    Username = username,
                    Typ = type,
                    AuthData = authData
                };
            }

            public LoginCredentials GetCredentials()
            {
                return _loginCredentials;
            }

            public Builder Credentials(LoginCredentials credentials)
            {
                _loginCredentials = credentials;
                return this;
            }

            public Builder Stored()
            {
                if (!Conf.StoreCredentials) throw new Exception("Illegal state! Credentials storing not enabled!");
                return Stored(Conf.StoredCredentialsFile);
            }

            public Builder Stored(String storedCredentials)
            {
                FileStream fileStream = File.OpenRead(storedCredentials);
                StreamReader reader = new StreamReader(fileStream);
                JObject obj = JObject.Parse(reader.ReadToEnd());
                
                _loginCredentials = new LoginCredentials
                {
                    Typ = (AuthenticationType)Enum.Parse(typeof(AuthenticationType), obj["type"].ToString()),
                    Username = obj["username"].ToString(),
                    AuthData = Base64.Decode(obj["credentials"].ToString()),
                };
                
                reader.Close();

                return this;
            }

            public Builder OAuth()
            {
                if (Conf.StoreCredentials && File.Exists(Conf.StoredCredentialsFile))
                    return Stored();

                _oAuth = new OAuth(MercuryRequests.KEYMASTER_CLIENT_ID, new Uri("http://127.0.0.1:5588/login"));
                _loginCredentials = _oAuth.flow();
                
                return this;
            }

            public Builder Blob(String username, byte[] blob)
            {
                if (DeviceId == null)
                    throw new Exception("You must specify the device ID first.");
                
                _loginCredentials = DecryptBlob(DeviceId, username, blob);
                return this;
            }

            public Session Create()
            {
                if (_loginCredentials == null && _oAuth == null)
                    throw new Exception("You must select an authentication method.");

                if (_loginCredentials == null && _oAuth != null)
                    throw new Exception("OAuth did not succeed.");
                
                TimeProvider.init(Conf);
                
                ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;

                Session session = new Session(new Inner(DeviceType, DeviceName, DeviceId, PreferredLocale, Conf));
                session.Connect();
                session.Authenticate(_loginCredentials);
                session.GetApi().SetClientToken(ClientToken);
                return session;
            }
        }

        public class Configuration
        {
            // Time sync
            public TimeProvider.Method TimeSynchronizationMethod;
            public int TimeManualCorrection;

            // Cache
            public bool CacheEnabled;
            public String CacheDir;
            public bool DoCacheCleanUp;

            // Stored credentials
            public bool StoreCredentials;
            public String StoredCredentialsFile;

            // Fetching
            public bool RetryOnChunkError;

            // Network
            public int ConnectionTimeout;
            
            // Proxy
            public WebProxy Proxy;

            private Configuration(TimeProvider.Method timeSynchronizationMethod, int timeManualCorrection,
                bool cacheEnabled, String cacheDir, bool doCacheCleanUp,
                bool storeCredentials, String storedCredentialsFile,
                bool retryOnChunkError, int connectionTimeout, WebProxy proxy)
            {
                TimeSynchronizationMethod = timeSynchronizationMethod;
                TimeManualCorrection = timeManualCorrection;
                CacheEnabled = cacheEnabled;
                CacheDir = cacheDir;
                DoCacheCleanUp = doCacheCleanUp;
                StoreCredentials = storeCredentials;
                StoredCredentialsFile = storedCredentialsFile;
                RetryOnChunkError = retryOnChunkError;
                ConnectionTimeout = connectionTimeout;
                Proxy = proxy;
            }

            public class Builder
            {
                // Time sync
                private TimeProvider.Method _timeSynchronizationMethod = TimeProvider.Method.NTP;
                private int _timeManualCorrection;

                // Cache
                private bool _cacheEnabled = true;
                private String _cacheDir = "cache";
                private bool _doCacheCleanUp;

                // Stored credentials
                private bool _storeCredentials = true;
                private String _storedCredentialsFile = "credentials.json";

                // Fetching
                private bool _retryOnChunkError;

                // Network
                private int _connectionTimeout;
                
                // Proxy
                private WebProxy _proxy;

                public Builder()
                {
                }
                
                public Builder SetTimeSynchronizationMethod(TimeProvider.Method timeSynchronizationMethod)
                {
                    _timeSynchronizationMethod = timeSynchronizationMethod;
                    return this;
                }

                public Builder SetTimeManualCorrection(int timeManualCorrection)
                {
                    _timeManualCorrection = timeManualCorrection;
                    return this;
                }

                public Builder SetCacheEnabled(bool cacheEnabled)
                {
                    _cacheEnabled = cacheEnabled;
                    return this;
                }

                public Builder SetCacheDir(String cacheDir)
                {
                    _cacheDir = cacheDir;
                    return this;
                }

                public Builder SetDoCacheCleanUp(bool doCacheCleanUp)
                {
                    _doCacheCleanUp = doCacheCleanUp;
                    return this;
                }

                public Builder SetStoreCredentials(bool storeCredentials)
                {
                    _storeCredentials = storeCredentials;
                    return this;
                }

                public Builder SetStoredCredentialsFile(String storedCredentialsFile)
                {
                    _storedCredentialsFile = storedCredentialsFile;
                    return this;
                }

                public Builder SetRetryOnChunkError(bool retryOnChunkError)
                {
                    _retryOnChunkError = retryOnChunkError;
                    return this;
                }

                public Builder SetConnectionTimeout(int connectionTimeout)
                {
                    _connectionTimeout = connectionTimeout;
                    return this;
                }
                
                public Builder SetProxy(WebProxy proxy)
                {
                    _proxy = proxy;
                    return this;
                }

                public Configuration Build()
                {
                    return new Configuration(_timeSynchronizationMethod, _timeManualCorrection,
                        _cacheEnabled, _cacheDir, _doCacheCleanUp,
                        _storeCredentials, _storedCredentialsFile,
                        _retryOnChunkError, _connectionTimeout, _proxy);
                }
            }
        }

        public class SpotifyAuthenticationException : Exception
        {
            public SpotifyAuthenticationException(APLoginFailed loginFailed) : base(loginFailed.ErrorCode.ToString())
            {
            }
        }

        private class ProxySocket : Socket
        {
            private WebProxy _proxy;

            public ProxySocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType) : base(addressFamily, socketType, protocolType)
            {
            }

            public ProxySocket(SocketInformation socketInformation) : base(socketInformation)
            {
            }

            public void SetProxy(WebProxy proxy)
            {
                _proxy = proxy;
            }

            public new void Connect(string host, int port)
            {
                if (_proxy != null)
                {
                    
                    string connectRequest = $"CONNECT {host}:{port} HTTP/1.1\r\n" +
                                            $"Host: {host}:{port}\r\n" +
                                            "Connection: keep-alive\r\n";

                    if (_proxy.Credentials is NetworkCredential)
                    {
                        var credentials = (NetworkCredential)_proxy.Credentials;
                        connectRequest += $"Proxy-Authorization: Basic {Base64.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}"))}";
                    }

                    connectRequest += "\r\n";
                    
                    base.Connect(_proxy.Address.Host, _proxy.Address.Port);
                    
                    byte[] requestBytes = Encoding.ASCII.GetBytes(connectRequest);
                    Send(requestBytes);

                    byte[] responseBuffer = new byte[1024];
                    int bytesReceived = Receive(responseBuffer);
                    string response = Encoding.ASCII.GetString(responseBuffer, 0, bytesReceived);

                    if (!response.StartsWith("HTTP/1.1 200"))
                    {
                        throw new Exception($"Connection to proxy failed! Response: {response}");
                    }
                }
                else
                {
                    base.Connect(host, port);
                }
            }
        }
        
        private class ConnectionHolder
        {
            public ProxySocket Socket;
            public NetworkStream Stream;
            public BinaryReader In;
            public BinaryWriter Out;
            private static int _retries = 0;

            /// <exception cref="IOException"></exception>
            private ConnectionHolder(ProxySocket socket)
            {
                Socket = socket;
                Stream = new NetworkStream(Socket);
                In = new BinaryReader(Stream);
                Out = new BinaryWriter(Stream);
            }

            /// <exception cref="IOException"></exception>
            public static ConnectionHolder Create(String addr, Configuration conf)
            {
                String[] split = addr.Split(':');
                String apAddr = split[0];
                int apPort = int.Parse(split[1]);
                ProxySocket socket = new ProxySocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    if (conf.Proxy != null)
                    {
                        socket.SetProxy(conf.Proxy);
                    }
                    socket.Connect(apAddr, apPort);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionRefused && !(_retries > 5))
                    {
                        _retries++;
                        return Create(addr, conf);
                    }

                    throw;
                }

                return new ConnectionHolder(socket);
            }
        }

        public class Receiver
        {
            private Thread _thread;
            private volatile bool _running = true;
            private Session _sessionParent;

            internal Receiver(Session sessionParent)
            {
                _sessionParent = sessionParent;
                _thread = new Thread(Run);
                _thread.Name = "session-packet-receiver";
                _thread.Start();
            }

            public void Stop()
            {
                _running = false;
                _thread.Interrupt();
            }

            public void Run()
            {
                LOGGER.Debug("Session.Receiver started");

                while (_running)
                {
                    Packet packet;
                    Packet.Type cmd;
                    try
                    {
                        packet = _sessionParent._cipherPair.ReceiveEncoded(_sessionParent._conn.In);
                        cmd = Packet.Parse(packet._cmd);
                        if (cmd == Packet.Type.NULL)
                        {
                            LOGGER.Info(string.Format("Skipping unknown command (cmd: 0x{0}, payload: {1})",
                                BitConverter.ToString(new byte[] {packet._cmd}), Utils.bytesToHex(packet._payload)));
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is IOException || ex is GeneralSecurityException)) throw;
                        
                        if (_running && !_sessionParent._closing)
                        {
                            LOGGER.Error("Failed reading packet!", ex);
                            _sessionParent.Reconnect();
                        }

                        break;
                    }

                    if (!_running) break;

                    switch (cmd)
                    {
                        case Packet.Type.Ping:
                            if (_sessionParent._scheduledReconnect != null) _sessionParent._scheduledReconnect.Cancel();
                            _sessionParent._scheduler.schedule(_sessionParent._scheduledReconnect);

                            TimeProvider.updateWithPing(packet._payload);

                            try
                            {
                                _sessionParent.Send(Packet.Type.Pong, packet._payload);
                            }
                            catch (IOException ex)
                            {
                                LOGGER.Error("Failed sending Pong!", ex);
                            }

                            break;
                        case Packet.Type.PongAck:
                            // Silent
                            break;
                        case Packet.Type.CountryCode:
                            _sessionParent._countryCode = Encoding.UTF8.GetString(packet._payload);
                            LOGGER.Info("Received CountryCode: " + _sessionParent._countryCode);
                            break;
                        case Packet.Type.LicenseVersion:
                            BinaryReader licenseVersion = new BinaryReader(new MemoryStream(packet._payload));
                            short id = licenseVersion.ReadInt16();
                            if (id != 0)
                            {
                                byte[] buffer = new byte[licenseVersion.ReadByte()];
                                licenseVersion.ReadFully(buffer);
                                LOGGER.Info("Received LicenseVersion: " + id + ", " + Encoding.UTF8.GetString(buffer));
                            }
                            else
                            {
                                LOGGER.Info("Received LicenseVersion: " + id);
                            }

                            break;
                        case Packet.Type.Unknown_0x10:
                            LOGGER.Debug("Received 0x10: " + Utils.bytesToHex(packet._payload));
                            break;
                        case Packet.Type.MercurySub:
                        case Packet.Type.MercuryUnsub:
                        case Packet.Type.MercuryEvent:
                        case Packet.Type.MercuryReq:
                            _sessionParent.GetMercury().Dispatch(packet);
                            break;
                        case Packet.Type.AesKey:
                        case Packet.Type.AesKeyError:
                            _sessionParent.GetAudioKey().Dispatch(packet);
                            break;
                        case Packet.Type.ChannelError:
                        case Packet.Type.StreamChunkRes:
                            _sessionParent.GetChannel().Dispatch(packet);
                            break;
                        case Packet.Type.ProductInfo:
                            try
                            {
                                if (_sessionParent._userAttributes.Count == 0)
                                {
                                    _sessionParent.ParseProductInfo(packet._payload);
                                }
                            }
                            catch (IOException ex) {
                                LOGGER.Warn("Failed parsing product info!", ex);
                            }
                            break;
                        default:
                            LOGGER.Info("Skipping " + cmd);
                            break;
                    }
                }

                LOGGER.Debug("Session.Receiver stopped");
            }
        }
    }
}