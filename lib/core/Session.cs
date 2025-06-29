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

        private static byte[] serverKey =
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

        private ApResolver apResolver;
        private DiffieHellman keys;

        private Inner inner;

        private ScheduledExecutorService scheduler = new ScheduledExecutorService();
        private Object authLock = new Object();
        private bool authLockState = false;
        private static HttpClient client;
        private List<CloseListener> closeListeners = new List<CloseListener>();
        private List<ReconnectionListener> reconnectionListeners = new List<ReconnectionListener>();
        private Object reconnectionListenersLock = new Object();
        private Dictionary<String, String> userAttributtes = new Dictionary<String, String>();
        private static ConnectionHolder conn;
        private static volatile CipherPair cipherPair;
        private Receiver receiver;
        private APWelcome apWelcome;

        private String countryCode = null;
        private volatile bool closed = false;
        private volatile bool closing = false;
        private volatile ScheduledExecutorService.ScheduledFuture<int> _scheduledReconnect;

        private Session(Inner inner)
        {
            this.inner = inner;
            keys = new DiffieHellman(inner.random);
            client = createClient(inner.conf);
            apResolver = new ApResolver(client);
            String addr = apResolver.getRandomAccesspoint();
            conn = ConnectionHolder.create(addr, inner.conf);

            _scheduledReconnect = new ScheduledExecutorService.ScheduledFuture<int>(() =>
                {
                    LOGGER.Warn("Socket timed out. Reconnecting...");
                    reconnect();
                    return 0;
                }, 2 * 60 + configuration().connectionTimeout);

            LOGGER.Info(String.Format("Created new session! (deviceId: {0}, ap: {1}, proxy: {2})", inner.deviceId, addr,
                false));
        }

        private static HttpClient createClient(Configuration configuration)
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

        private static int readBlobInt(byte[] buffer)
        {
            int pos = 0;
            int lo = buffer[pos];
            pos++;
            if ((lo & 0x80) == 0) return lo;
            int hi = buffer[pos];
            return lo & 0x7f | hi << 7;
        }

        private void connect()
        {
            MemoryStream accStream = new MemoryStream();
            BinaryWriter acc = new BinaryWriter(accStream);

            byte[] nonce = new byte[0x10];
            inner.random.GetBytes(nonce);

            ClientHello clientHello = new ClientHello
            {
                BuildInfo = Version.standardBuildInfo(),
                CryptosuitesSupporteds = { Cryptosuite.CryptoSuiteShannon },
                LoginCryptoHello = new LoginCryptoHelloUnion
                {
                    DiffieHellman = new LoginCryptoDiffieHellmanHello
                    {
                        Gc = keys.PublicKeyArray(),
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
            conn._out.Write((byte)0);
            conn._out.Write((byte)4);
            conn._out.WriteBigEndian(length); 
            conn._out.Write(clientHelloBytes);
            conn._out.Flush();
            
            acc.Write((byte)0);
            acc.Write((byte)4);
            acc.WriteBigEndian(length);
            acc.Write(clientHelloBytes);
            
            byte[] apResponseLengthBytes = conn._in.ReadBytes(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(apResponseLengthBytes);
            int apResponseLength = BitConverter.ToInt32(apResponseLengthBytes, 0);
            acc.WriteBigEndian(apResponseLength);

            byte[] apResponseMessageBytes = new byte[apResponseLength - 4];
            conn._in.ReadFully(apResponseMessageBytes);
            acc.Write(apResponseMessageBytes);
            
            MemoryStream apResponseStream = new MemoryStream();
            apResponseStream.Write(apResponseMessageBytes, 0, apResponseMessageBytes.Length);
            apResponseStream.Position = 0;
            APResponseMessage apResponseMessage = Serializer.Deserialize<APResponseMessage>(apResponseStream);
            byte[] sharedKey = Utils.toByteArray(keys.ComputeSharedKey(apResponseMessage.Challenge.LoginCryptoChallenge.DiffieHellman.Gs));

            RsaKeyParameters publicKey = new RsaKeyParameters(
                false,
                new BigInteger(1, serverKey),
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
            conn._out.WriteBigEndian(length);
            conn._out.Write(clientResponsePlaintextBytes);
            conn._out.Flush();
            
            try
            {
                byte[] scrap = new byte[4];
                conn.stream.ReadTimeout = 300;
                int read = conn._in.Read(scrap, 0, scrap.Length); 
                if (read == scrap.Length)
                {
                    length = (scrap[0] << 24) | (scrap[1] << 16) | (scrap[2] << 8) | (scrap[3] & 0xFF);
                    byte[] payload = new byte[length - 4];
                    conn._in.ReadFully(payload);
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
                conn.stream.ReadTimeout = -1;
            }
            lock (authLock)
            {
                cipherPair = new CipherPair(
                    Arrays.CopyOfRange(dataArray, 20, 52), 
                    Arrays.CopyOfRange(dataArray, 52, 84)
                );
                authLockState = true;
            }
            
            LOGGER.Info("Connected successfully!");
        }
        
        private void authenticate(LoginCredentials credentials)
        {
            authenticatePartial(credentials, false);

            if (credentials.GetType() == AuthenticationType.AuthenticationSpotifyToken.GetType())
            {
                reconnect();
            }
        }

        private void authenticatePartial(LoginCredentials credentials, bool removeLock)
        { 
            if (conn == null || cipherPair == null) throw new Exception("Illegal state! Connection not established!");
            
            ClientResponseEncrypted clientResponseEncrypted = new ClientResponseEncrypted
            {
                LoginCredentials = credentials,
                SystemInfo = new SystemInfo
                {
                    Os = Os.OsUnknown,
                    CpuFamily = CpuFamily.CpuUnknown,
                    SystemInformationString = Version.systemInfoString(),
                    DeviceId = inner.deviceId
                },
                VersionString = Version.versionString()
            };
            
            MemoryStream clientResponseEncryptedStream = new MemoryStream();
            Serializer.Serialize(clientResponseEncryptedStream, clientResponseEncrypted);
            
            sendUnchecked(Packet.Type.Login, clientResponseEncryptedStream.ToArray());
            
            Packet packet = cipherPair.ReceiveEncoded(conn._in);
            if (packet.Is(Packet.Type.APWelcome))
            {
                apWelcome = Serializer.Deserialize<APWelcome>(new MemoryStream(packet._payload));

                receiver = new Receiver(this);

                byte[] bytes0x0f = new byte[20];
                random().GetBytes(bytes0x0f);
                sendUnchecked(Packet.Type.Unknown_0x0f, bytes0x0f);

                byte[] preferredLocaleBytes = Encoding.UTF8.GetBytes(inner.preferredLocale);
                MemoryStream preferredLocale = new MemoryStream();
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x10);
                preferredLocale.WriteByte(0x0);
                preferredLocale.WriteByte(0x02);
                preferredLocale.Write(preferredLocaleBytes, 0, preferredLocaleBytes.Length);
                sendUnchecked(Packet.Type.PreferredLocale, preferredLocale.ToArray());

                if (removeLock)
                {
                    lock (authLock)
                    {
                        authLockState = false;
                        Monitor.PulseAll(authLock);
                    }
                }

                if (inner.conf.storeCredentials)
                {
                    byte[] reusable = apWelcome.ReusableAuthCredentials;
                    AuthenticationType reusableType = apWelcome.ReusableAuthCredentialsType;
                    
                    JObject obj = new JObject();
                    obj["username"] = apWelcome.CanonicalUsername;
                    obj["credentials"] = Base64.ToBase64String(reusable);
                    obj["type"] = reusableType.ToString();

                    if (inner.conf.storedCredentialsFile == null) throw new Exception("Illegal argument");
                    FileStream credentialsFileStream = File.OpenWrite(inner.conf.storedCredentialsFile);
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
            LOGGER.Info("Closing session. (deviceId: " + inner.deviceId + ")");

            // if (scheduledReconnect != null) scheduledReconnect.cancel(true);
            
            closing = true;
            
            // scheduler.shutdownNow
            
            
        }

        private void sendUnchecked(Packet.Type cmd, byte[] payload)
        {
            if (conn == null) throw new Exception("Illegal state! Cannot write to missing connection.");

            cipherPair.SendEncoded(conn._out, (byte)cmd, payload);
        }

        private void waitAuthLock()
        {
            if (closing && conn == null)
            {
                LOGGER.Debug("Connection was broken while closing.");
                return;
            }

            if (closed) throw new Exception("Illegal state! Session is closed!");

            lock (authLock)
            {
                if (cipherPair == null)
                {
                    Monitor.Wait(authLock);
                }
            }
        }

        public void Send(Packet.Type cmd, byte[] payload)
        {
            if (closing && conn == null)
            {
                LOGGER.Debug("Connection was broken while closing.");
                return;
            }
            
            if (closed) throw new Exception("Illegal state! Session is closed!");

            lock (authLock)
            {
                if (cipherPair == null)
                {
                    Monitor.Wait(authLock);
                }
                
                sendUnchecked(cmd, payload);
            }
        }

        public ApResolver APResolver()
        {
            return apResolver;
        }

        public HttpClient Client()
        {
            return client;
        }

        public RandomNumberGenerator random()
        {
            return inner.random;
        }

        public Configuration configuration()
        {
            return inner.conf;
        }

        private void reconnect()
        {
            if (closing) return;

            lock (reconnectionListenersLock)
            {
                reconnectionListeners.ForEach(l => l.onConnectionDropped());
            }

            try
            {
                if (conn != null)
                {
                    receiver.stop();
                    conn.socket.Close();
                }

                apResolver.refreshPool();

                conn = ConnectionHolder.create(apResolver.getRandomAccesspoint(), inner.conf);
                connect();
                authenticatePartial(
                    new LoginCredentials
                    {
                        Username = apWelcome.CanonicalUsername,
                        Typ = apWelcome.ReusableAuthCredentialsType,
                        AuthData = apWelcome.ReusableAuthCredentials
                    }, true
                );
                
                LOGGER.Info("Re-authenticated as " + apWelcome.CanonicalUsername + "!");

                lock (reconnectionListenersLock)
                {
                    reconnectionListeners.ForEach(l => l.onConnectionEstablished());
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is SpotifyAuthenticationException)
                {
                    conn = null;
                    LOGGER.Error("Failed reconnecting, retrying in 10 seconds...", ex);

                    scheduler.schedule(
                        new ScheduledExecutorService.ScheduledFuture<int>(
                            () =>
                            {
                                reconnect();
                                return 0;
                            }, 10
                        )
                    );
                }
                else throw;
            }
        }

        private void parseProductInfo(byte[] payload)
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
                userAttributtes.Add(node.Name, node.InnerText);
            }
            
            String userAttributesDebugString = "{" + string.Join(",", userAttributtes.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}";
            LOGGER.Debug("Parsed product info: " + userAttributesDebugString);
        }

        public interface ReconnectionListener
        {
            void onConnectionDropped();

            void onConnectionEstablished();
        }

        public interface CloseListener
        {
            void onClose();
        }

        private class Inner
        {
            public DeviceType deviceType;
            public String deviceName;
            public RandomNumberGenerator random;
            public String deviceId;
            public Session.Configuration conf;
            public String preferredLocale;

            public Inner(DeviceType deviceType, String deviceName, String deviceId, String preferredLocale, Configuration conf)
            {
                random = RandomNumberGenerator.Create();
                this.preferredLocale = preferredLocale;
                this.conf = conf;
                this.deviceType = deviceType;
                this.deviceName = deviceName; 
                this.deviceId = String.IsNullOrEmpty(deviceId) ? Utils.randomHexString(random, 40).ToLower() : deviceId;
            }
        }

        public abstract class AbsBuilder<T>
        {
            protected Configuration conf;
            protected String deviceId = null;
            protected String clientToken = null;
            protected String deviceName = "librespot-java";
            protected DeviceType deviceType = DeviceType.Computer;
            protected String preferredLocale = "en";

            public AbsBuilder(Configuration conf)
            {
                this.conf = conf;
            }

            protected AbsBuilder() : this(new Configuration.Builder().build())
            {
            }

            /**
             * Sets the preferred locale for the user.
             *
             * @param locale A 2 chars locale code
             */
            public AbsBuilder<T> setPreferredLocale(String locale)
            {
                if (locale.Length != 2)
                    throw new Exception("Invalid locale: " + locale);

                preferredLocale = locale;
                return this;
            }

            /**
             * Sets the device name that will appear on Spotify Connect.
             *
             * @param deviceName The device name
             */
            public AbsBuilder<T> setDeviceName(String deviceName)
            {
                this.deviceName = deviceName;
                return this;
            }

            /**
             * Sets the device ID. If not provided or empty will be generated randomly.
             *
             * @param deviceId A 40 chars string
             */
            public AbsBuilder<T> setDeviceId(String deviceId)
            {
                if (deviceId != null && deviceId.Length != 40)
                    throw new Exception("Device ID must be 40 chars long.");

                this.deviceId = deviceId;
                return this;
            }

            /**
             * Sets the client token. If empty, it will be retrieved.
             *
             * @param token A 168 bytes Base64 encoded string
             */
            public AbsBuilder<T> setClientToken(String token)
            {
                clientToken = token;
                return this;
            }

            /**
             * Sets the device type.
             *
             * @param deviceType The {@link com.spotify.connectstate.Connect.DeviceType}
             */
            public AbsBuilder<T> setDeviceType(DeviceType deviceType)
            {
                this.deviceType = deviceType;
                return this;
            }
        }

        public class Builder : AbsBuilder<Builder>
        {
            private LoginCredentials loginCredentials = null;

            public Builder(Configuration conf) : base(conf)
            {
            }

            public Builder()
            {
            }

            public Builder stored()
            {
                if (!conf.storeCredentials) throw new Exception("Illegal state! Credentials storing not enabled!");
                return stored(conf.storedCredentialsFile);
            }

            public Builder stored(String storedCredentials)
            {
                FileStream fileStream = File.OpenRead(storedCredentials);
                StreamReader reader = new StreamReader(fileStream);
                JObject obj = JObject.Parse(reader.ReadToEnd());
                
                loginCredentials = new LoginCredentials
                {
                    Typ = (AuthenticationType)Enum.Parse(typeof(AuthenticationType), obj["type"].ToString()),
                    Username = obj["username"].ToString(),
                    AuthData = Base64.Decode(obj["credentials"].ToString()),
                };
                
                reader.Close();

                return this;
            }

            public Builder oauth()
            {
                if (conf.storeCredentials && File.Exists(conf.storedCredentialsFile))
                    return stored();

                OAuth oauth = new OAuth(MercuryRequests.KEYMASTER_CLIENT_ID, new Uri("http://127.0.0.1:5588/login"));
                loginCredentials = oauth.flow();
                
                return this;
            }

            public Session create()
            {
                if (loginCredentials == null)
                    throw new Exception("You must select an authentication method.");
                
                TimeProvider.init(conf);

                Session session = new Session(new Inner(deviceType, deviceName, deviceId, preferredLocale, conf));
                session.connect();
                session.authenticate(loginCredentials);
                //session.api().setClientToken(clientToken);
                return session;
            }
        }

        public class Configuration
        {
            // Time sync
            public TimeProvider.Method timeSynchronizationMethod;
            public int timeManualCorrection;

            // Cache
            public bool cacheEnabled;
            public String cacheDir;
            public bool doCacheCleanUp;

            // Stored credentials
            public bool storeCredentials;
            public String storedCredentialsFile;

            // Fetching
            public bool retryOnChunkError;

            // Network
            public int connectionTimeout;

            private Configuration(TimeProvider.Method timeSynchronizationMethod, int timeManualCorrection,
                bool cacheEnabled, String cacheDir, bool doCacheCleanUp,
                bool storeCredentials, String storedCredentialsFile,
                bool retryOnChunkError, int connectionTimeout)
            {
                this.timeSynchronizationMethod = timeSynchronizationMethod;
                this.timeManualCorrection = timeManualCorrection;
                this.cacheEnabled = cacheEnabled;
                this.cacheDir = cacheDir;
                this.doCacheCleanUp = doCacheCleanUp;
                this.storeCredentials = storeCredentials;
                this.storedCredentialsFile = storedCredentialsFile;
                this.retryOnChunkError = retryOnChunkError;
                this.connectionTimeout = connectionTimeout;
            }

            public class Builder
            {
                // Time sync
                private TimeProvider.Method timeSynchronizationMethod = TimeProvider.Method.NTP;
                private int timeManualCorrection;

                // Cache
                private bool cacheEnabled = true;
                private String cacheDir = "cache";
                private bool doCacheCleanUp;

                // Stored credentials
                private bool storeCredentials = true;
                private String storedCredentialsFile = "credentials.json";

                // Fetching
                private bool retryOnChunkError;

                // Network
                private int connectionTimeout;

                public Builder()
                {
                }
                
                public Builder setTimeSynchronizationMethod(TimeProvider.Method timeSynchronizationMethod)
                {
                    this.timeSynchronizationMethod = timeSynchronizationMethod;
                    return this;
                }

                public Builder setTimeManualCorrection(int timeManualCorrection)
                {
                    this.timeManualCorrection = timeManualCorrection;
                    return this;
                }

                public Builder setCacheEnabled(bool cacheEnabled)
                {
                    this.cacheEnabled = cacheEnabled;
                    return this;
                }

                public Builder setCacheDir(String cacheDir)
                {
                    this.cacheDir = cacheDir;
                    return this;
                }

                public Builder setDoCacheCleanUp(bool doCacheCleanUp)
                {
                    this.doCacheCleanUp = doCacheCleanUp;
                    return this;
                }

                public Builder setStoreCredentials(bool storeCredentials)
                {
                    this.storeCredentials = storeCredentials;
                    return this;
                }

                public Builder setStoredCredentialsFile(String storedCredentialsFile)
                {
                    this.storedCredentialsFile = storedCredentialsFile;
                    return this;
                }

                public Builder setRetryOnChunkError(bool retryOnChunkError)
                {
                    this.retryOnChunkError = retryOnChunkError;
                    return this;
                }

                public Builder setConnectionTimeout(int connectionTimeout)
                {
                    this.connectionTimeout = connectionTimeout;
                    return this;
                }

                public Configuration build()
                {
                    return new Configuration(timeSynchronizationMethod, timeManualCorrection,
                        cacheEnabled, cacheDir, doCacheCleanUp,
                        storeCredentials, storedCredentialsFile,
                        retryOnChunkError, connectionTimeout);
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
            public Socket socket;
            public NetworkStream stream;
            public BinaryReader _in;
            public BinaryWriter _out;

            /// <exception cref="IOException"></exception>
            private ConnectionHolder(Socket socket)
            {
                this.socket = socket;
                stream = new NetworkStream(this.socket);
                _in = new BinaryReader(stream);
                _out = new BinaryWriter(stream);
            }

            /// <exception cref="IOException"></exception>
            public static ConnectionHolder create(String addr, Configuration conf)
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
                _thread = new Thread(run);
                _thread.Name = "session-packet-receiver";
                _thread.Start();
            }

            public void stop()
            {
                _running = false;
                _thread.Interrupt();
            }

            public void run()
            {
                LOGGER.Debug("Session.Receiver started");

                while (_running)
                {
                    Packet packet;
                    Packet.Type cmd;
                    try
                    {
                        packet = cipherPair.ReceiveEncoded(conn._in);
                        cmd = Packet.Parse(packet._cmd);
                        if (cmd == Packet.Type.NULL)
                        {
                            LOGGER.Info(string.Format("Skipping unknown command (cmd: 0x{0}, payload: {1})",
                                BitConverter.ToString(new byte[] {packet._cmd}), Utils.bytesToHex(packet._payload)));
                            continue;
                        }
                    }
                    catch (GeneralSecurityException ex) {
                        if (_running && !_sessionParent.closing)
                        {
                            LOGGER.Error("Failed reading packet!", ex);
                            _sessionParent.reconnect();
                        }

                        break;
                    }

                    if (!_running) break;

                    switch (cmd)
                    {
                        case Packet.Type.Ping:
                            if (_sessionParent._scheduledReconnect != null) _sessionParent._scheduledReconnect.Cancel();
                            _sessionParent.scheduler.schedule(_sessionParent._scheduledReconnect);

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
                            _sessionParent.countryCode = Encoding.UTF8.GetString(packet._payload);
                            LOGGER.Info("Received CountryCode: " + _sessionParent.countryCode);
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
                        /*case Packet.Type.MercuryReq:
                            mercury().dispatch(packet);
                            break;
                        case Packet.Type.AesKey:
                        case Packet.Type.AesKeyError:
                            audioKey().dispatch(packet);
                            break;
                        case Packet.Type.ChannelError:
                        case Packet.Type.StreamChunkRes:
                            channel().dispatch(packet);
                            break;
                        case Packet.Type.ProductInfo:
                            try
                            {
                                _sessionParent.parseProductInfo(packet.payload);
                            }
                            catch (Exception ex) {
                                LOGGER.Warn("Failed parsing product info!", ex); 
                            }
                            break;*/
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