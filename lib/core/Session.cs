using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using Connectstate;
using EasyHttp.Http;
using EasyHttp.Http.Injection;
using lib.common;
using lib.crypto;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;
using ProtoBuf;
using Spotify;
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
        private bool _authLockState = false;
        private HttpClient _client;
        private List<CloseListener> _closeListeners = new List<CloseListener>();
        private List<ReconnectionListener> _reconnectionListeners = new List<ReconnectionListener>();
        private Object _reconnectionListenersLock = new Object();
        private Dictionary<String, String> _userAttributtes = new Dictionary<String, String>();
        private ConnectionHolder _conn;
        private volatile CipherPair _cipherPair;
        private Receiver _receiver;
        private APWelcome _apWelcome;
        private MercuryClient _mercuryClient;

        private String _countryCode = null;
        private volatile bool _closed = false;
        private volatile bool _closing = false;
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

            client.RegisteredInterceptions.Add(new HttpRequestInterception(request =>
            {
                if (request.Data == null || !request.RawHeaders.ContainsKey("Content-Encoding"))
                    return false;
                
                request.ContentEncoding = "gzip";
                request.Data = new GZipStream(request.Data as Stream, CompressionMode.Compress);

                return true;
            }));

            return client;
        }

        private static int ReadBlobInt(byte[] buffer)
        {
            int pos = 0;
            int lo = buffer[pos];
            pos++;
            if ((lo & 0x80) == 0) return lo;
            int hi = buffer[pos];
            return lo & 0x7f | hi << 7;
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
            
            Console.WriteLine(challenge.Length);
            
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
            {
                Reconnect();
            }

            lock (_authLock)
            {
                _mercuryClient = new MercuryClient(this);
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

            
            //ToDo: Implement this fully
            
            
        }

        private void SendUnchecked(Packet.Type cmd, byte[] payload)
        {
            if (_conn == null) throw new Exception("Illegal state! Cannot write to missing connection.");

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
                if (_cipherPair == null)
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
                if (_cipherPair == null)
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
            return _mercuryClient;
        }

        public HttpClient GetClient()
        {
            return _client;
        }

        public RandomNumberGenerator GetRandom()
        {
            return _inner.Random;
        }

        public Configuration GetConfiguration()
        {
            return _inner.Conf;
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

        private void ParseProductInfo(byte[] payload)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(Encoding.UTF8.GetString(payload));

            if (doc.GetElementsByTagName("product").Count == 0) return;
            XmlNode products = doc.GetElementsByTagName("products")[0];

            if (doc.ChildNodes.Count == 0) return;
            XmlNode product = doc.ChildNodes[0];

            XmlNodeList properties = product.ChildNodes;
            for (int i = 0; i < products.ChildNodes.Count; i++)
            {
                XmlNode node = properties[i];
                _userAttributtes.Add(node.Name, node.InnerText);
            }
            
            String userAttributesDebugString = "{" + string.Join(",", _userAttributtes.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}";
            LOGGER.Debug("Parsed product info: " + userAttributesDebugString);
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
            public Session.Configuration Conf;
            public String PreferredLocale;

            public Inner(DeviceType deviceType, String deviceName, String deviceId, String preferredLocale, Configuration conf)
            {
                Random = RandomNumberGenerator.Create();
                preferredLocale = preferredLocale;
                Conf = conf;
                DeviceType = deviceType;
                DeviceName = deviceName; 
                DeviceId = String.IsNullOrEmpty(deviceId) ? Utils.randomHexString(Random, 40).ToLower() : deviceId;
            }
        }

        public abstract class AbsBuilder<T>
        {
            protected Configuration Conf;
            protected String DeviceId = null;
            protected String ClientToken = null;
            protected String DeviceName = "librespot-java";
            protected DeviceType DeviceType = DeviceType.Computer;
            protected String PreferredLocale = "en";

            public AbsBuilder(Configuration conf)
            {
                Conf = conf;
            }

            protected AbsBuilder() : this(new Configuration.Builder().Build())
            {
            }

            /**
             * Sets the preferred locale for the user.
             *
             * @param locale A 2 chars locale code
             */
            public AbsBuilder<T> SetPreferredLocale(String locale)
            {
                if (locale.Length != 2)
                    throw new Exception("Invalid locale: " + locale);

                PreferredLocale = locale;
                return this;
            }

            /**
             * Sets the device name that will appear on Spotify Connect.
             *
             * @param deviceName The device name
             */
            public AbsBuilder<T> SetDeviceName(String deviceName)
            {
                DeviceName = deviceName;
                return this;
            }

            /**
             * Sets the device ID. If not provided or empty will be generated randomly.
             *
             * @param deviceId A 40 chars string
             */
            public AbsBuilder<T> SetDeviceId(String deviceId)
            {
                if (deviceId != null && deviceId.Length != 40)
                    throw new Exception("Device ID must be 40 chars long.");

                DeviceId = deviceId;
                return this;
            }

            /**
             * Sets the client token. If empty, it will be retrieved.
             *
             * @param token A 168 bytes Base64 encoded string
             */
            public AbsBuilder<T> SetClientToken(String token)
            {
                ClientToken = token;
                return this;
            }

            /**
             * Sets the device type.
             *
             * @param deviceType The {@link com.spotify.connectstate.Connect.DeviceType}
             */
            public AbsBuilder<T> SetDeviceType(DeviceType deviceType)
            {
                DeviceType = deviceType;
                return this;
            }
        }

        public class Builder : AbsBuilder<Builder>
        {
            private LoginCredentials _loginCredentials = null;

            public Builder(Configuration conf) : base(conf)
            {
            }

            public Builder()
            {
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

                OAuth oauth = new OAuth(MercuryRequests.KEYMASTER_CLIENT_ID, new Uri("http://127.0.0.1:5588/login"));
                _loginCredentials = oauth.flow();
                
                return this;
            }

            public Session Create()
            {
                if (_loginCredentials == null)
                    throw new Exception("You must select an authentication method.");
                
                TimeProvider.init(Conf);

                Session session = new Session(new Inner(DeviceType, DeviceName, DeviceId, PreferredLocale, Conf));
                session.Connect();
                session.Authenticate(_loginCredentials);
                //ToDo: session.api().setClientToken(clientToken);
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

            private Configuration(TimeProvider.Method timeSynchronizationMethod, int timeManualCorrection,
                bool cacheEnabled, String cacheDir, bool doCacheCleanUp,
                bool storeCredentials, String storedCredentialsFile,
                bool retryOnChunkError, int connectionTimeout)
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

                public Configuration Build()
                {
                    return new Configuration(_timeSynchronizationMethod, _timeManualCorrection,
                        _cacheEnabled, _cacheDir, _doCacheCleanUp,
                        _storeCredentials, _storedCredentialsFile,
                        _retryOnChunkError, _connectionTimeout);
                }
            }
        }

        public class SpotifyAuthenticationException : Exception
        {
            public SpotifyAuthenticationException(APLoginFailed loginFailed) : base(loginFailed.ErrorCode.ToString())
            {
            }
        }
        
        private class ConnectionHolder
        {
            public Socket Socket;
            public NetworkStream Stream;
            public BinaryReader In;
            public BinaryWriter Out;

            /// <exception cref="IOException"></exception>
            private ConnectionHolder(Socket socket)
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
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(apAddr, apPort);
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
                    catch (GeneralSecurityException ex) {
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
                        /*case Packet.Type.AesKey:
                        case Packet.Type.AesKeyError:
                            audioKey().dispatch(packet);
                            break;
                        case Packet.Type.ChannelError:
                        case Packet.Type.StreamChunkRes:
                            channel().dispatch(packet);
                            break;*/
                        case Packet.Type.ProductInfo:
                            try
                            {
                                _sessionParent.ParseProductInfo(packet._payload);
                            }
                            catch (Exception ex) {
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