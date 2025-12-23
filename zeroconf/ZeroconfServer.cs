using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using api.server;
using Connectstate;
using lib.core;
using lib.crypto;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Encoders;
using zeroconf.zeroconf;
using Version = lib.Version;

namespace zeroconf
{
    public class ZeroconfServer : IDisposable
    {
        public static String SERVICE = "spotify-connect";
        private static readonly int MAX_PORT = 65536;
        private static readonly int MIN_PORT = 1024;
        private static readonly byte[] EOL = new byte[] { (byte)'\r', (byte)'\n' };

        private static readonly JObject DEFAULT_GET_INFO_FIELDS = new JObject()
        {
            { "status", 101 },
            { "statusString", "OK" },
            { "spotifyError", 0 },
            { "version", "2.7.1" },
            { "libraryVersion", Version.versionNumber() },
            { "accountREQ", "FREE" },
            { "brandDisplayName", "librespot-org" },
            { "modelDisplayName", "librespot-csharp" },
            { "voiceSupport", "NO" },
            { "availability", "" },
            { "productID", 0 },
            { "tokenType", "default" },
            { "groupStatus", "NONE" },
            { "resolverVersion", "0" },
            { "scope", "streaming,client-authorization-universal" },
        };

        private static readonly JObject DEFAULT_SUCCESSFUL_ADD_USER = new JObject()
        {
            { "status", 101 },
            { "spotifyError", 0 },
            { "statusString", "OK" }
        };

        private static readonly byte[][] VIRTUAL_INTERFACES = new byte[][]
        {
            new byte[] { (byte)0x00, (byte)0x0F, (byte)0x4B }, // Virtual Iron Software, Inc.
            new byte[] { (byte)0x00, (byte)0x13, (byte)0x07 }, // Paravirtual Corporation
            new byte[] { (byte)0x00, (byte)0x13, (byte)0xBE }, // Virtual Conexions
            new byte[] { (byte)0x00, (byte)0x21, (byte)0xF6 }, // Virtual Iron Software
            new byte[] { (byte)0x00, (byte)0x24, (byte)0x0B }, // Virtual Computer Inc.
            new byte[] { (byte)0x00, (byte)0xA0, (byte)0xB1 }, // First Virtual Corporation
            new byte[] { (byte)0x00, (byte)0xE0, (byte)0xC8 }, // Virtual access, ltd.
            new byte[] { (byte)0x54, (byte)0x52, (byte)0x00 }, // Linux kernel virtual machine (kvm)
            new byte[] { (byte)0x00, (byte)0x21, (byte)0xF6 }, // Oracle Corporation
            new byte[] { (byte)0x18, (byte)0x92, (byte)0x2C }, // Virtual Instruments
            new byte[] { (byte)0x3C, (byte)0xF3, (byte)0x92 }, // VirtualTek. Co. Ltd.
            new byte[] { (byte)0x00, (byte)0x05, (byte)0x69 }, // VMWare 1
            new byte[] { (byte)0x00, (byte)0x0C, (byte)0x29 }, // VMWare 2
            new byte[] { (byte)0x00, (byte)0x50, (byte)0x56 }, // VMWare 3
            new byte[] { (byte)0x00, (byte)0x1C, (byte)0x42 }, // Parallels
            new byte[] { (byte)0x00, (byte)0x03, (byte)0xFF }, // Microsoft Virtual PC
            new byte[] { (byte)0x00, (byte)0x16, (byte)0x3E }, // Red Hat Xen, Oracle VM, Xen Source, Novell Xen
            new byte[] { (byte)0x08, (byte)0x00, (byte)0x27 }, // VirtualBox
            new byte[] { (byte)0x00, (byte)0x15, (byte)0x5D }, // Hyper-V
        };

        private HttpRunner _runner;
        private readonly DiffieHellman _keys;
        private readonly List<SessionListener> _sessionListeners;
        private readonly Zeroconf _zeroconf;
        private readonly object _connectionLock = new object();
        private readonly Inner _inner;
        private volatile Session _session;
        private String _connectingUsername = null;
        private readonly Random _random = new Random();
        private static ILog LOGGER = LogManager.GetLogger(typeof(ZeroconfServer));

        internal ZeroconfServer(Inner inner, int listenPort, bool listenAllInterfaces, String[] interfacesList)
        {
            _inner = inner;
            _keys = new DiffieHellman(_inner._random);
            _sessionListeners = new List<SessionListener>();

            if (listenPort == -1)
                listenPort = _random.Next(MIN_PORT, MAX_PORT);

            _runner = new HttpRunner(listenPort, this);
            _runner.Start();

            List<NetworkInterface> nics;
            if (listenAllInterfaces)
            {
                nics = GetAllInterfaces();
            }
            else
            {
                if (interfacesList == null || interfacesList.Length == 0)
                {
                    nics = GetAllInterfaces();
                }
                else
                {
                    nics = new List<NetworkInterface>();
                    foreach (String str in interfacesList)
                    {
                        NetworkInterface nif = GetByName(str);
                        if (nif == null)
                        {
                            LOGGER.WarnFormat("Interface {0} doesn't exist.", str);
                            continue;
                        }

                        CheckInterface(nics, nif);
                    }
                }
            }

            _zeroconf = new Zeroconf();
            _zeroconf.SetLocalHostName(GetUsefulHostname());
            _zeroconf.SetUseIpv4(true).SetUseIpv6(false);
            _zeroconf.AddNetworkInterfaces(nics);

            Dictionary<String, String> txt = new Dictionary<string, string>()
            {
                { "CPath", "/" },
                { "VERSION", "1.0" },
                { "Stack", "SP" }
            };
            Service service = new Service(_inner._deviceName, SERVICE, listenPort);
            service.SetText(txt);

            _zeroconf.Announce(service);
        }

        public NetworkInterface GetByName(string name)
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return nic;
            }

            return null;
        }

        public static String GetUsefulHostname()
        {
            String host = Dns.GetHostName();
            if (Equals(host, "localhost"))
            {
                host = Convert.ToBase64String(Zeroconf.NextLongLocalThreadBytes()) + ".local";
                LOGGER.WarnFormat("Hostname cannot be 'localhost', temporary hostname: {0}", host);
                return host;
            }

            return host;
        }

        private static bool IsVirtual(NetworkInterface nif)
        {
            byte[] mac = nif.GetPhysicalAddress().GetAddressBytes();
            if (mac == null) return true;

            foreach (byte[] virtualMac in VIRTUAL_INTERFACES)
            {
                bool mismatch = false;
                for (int i = 0; i < Math.Min(virtualMac.Length, mac.Length); i++)
                {
                    if (virtualMac[i] != mac[i])
                    {
                        mismatch = true;
                        break;
                    }
                }

                if (!mismatch)
                    return true;
            }

            return false;
        }

        private static void CheckInterface(List<NetworkInterface> list, NetworkInterface nif)
        {
            if (nif.NetworkInterfaceType == NetworkInterfaceType.Loopback || IsVirtual(nif)) return;
            list.Add(nif);
        }

        private static List<NetworkInterface> GetAllInterfaces()
        {
            List<NetworkInterface> list = new List<NetworkInterface>();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface nic in nics)
                CheckInterface(list, nic);
            return list;
        }

        private static Dictionary<String, String> ParsePath(String path)
        {
            Uri url = new Uri("http://host" + path);
            var queryParameters = HttpUtility.ParseQueryString(url.Query);
            var map = new Dictionary<string, string>();
            foreach (string key in queryParameters.AllKeys)
                if (key != null)
                    map[key] = queryParameters[key];
            return map;
        }

        public void Dispose()
        {
            _zeroconf.Dispose();
            _runner.Dispose();
        }

        public void CloseSession()
        {
            if (_session == null) return;

            _sessionListeners.ForEach(l => l.SessionClosing(_session));
            _session.Dispose();
            _session = null;
        }

        internal bool HasValidSession()
        {
            bool valid = _session != null && _session.IsValid();
            if (!valid) _session = null;
            return valid;
        }

        internal void HandleGetInfo(HttpServerResponse response)
        {
            JObject info = new JObject(DEFAULT_GET_INFO_FIELDS);
            info["deviceID"] = _inner._deviceId;
            info["remoteName"] = _inner._deviceName;
            info["publicKey"] = Base64.Encode(_keys.PublicKeyArray());
            info["deviceType"] = _inner._deviceType.ToString();

            lock (_connectionLock)
            {
                info["activeUser"] = _connectingUsername != null
                    ? _connectingUsername
                    : (HasValidSession() ? _session.Username() : "");
            }
            
            response.ContentType = "application/json";
            response.Content = info.ToString(Newtonsoft.Json.Formatting.None);
        }

        internal void HandleAddUser(HttpServerResponse response, Dictionary<String, String> parameters)
        {
            parameters.TryGetValue("userName", out String userName);
            if (userName == null || userName.Equals(""))
            {
                LOGGER.Error("Missing userName!");
                return;
            }

            parameters.TryGetValue("blob", out String blobStr);
            if (blobStr == null || blobStr.Equals(""))
            {
                LOGGER.Error("Missing blob!");
                return;
            }

            parameters.TryGetValue("clientKey", out String clientKeyStr);
            if (clientKeyStr == null || clientKeyStr.Equals(""))
            {
                LOGGER.Error("Missing clientKey!");
                return;
            }

            lock (_connectionLock)
            {
                if (userName.Equals(_connectingUsername))
                {
                    LOGGER.InfoFormat("{0} is already trying to connect.", userName);
                    response.StatusCode = HttpStatusCode.Forbidden;
                    return;
                }
            }

            byte[] sharedKey = _keys.ComputeSharedKey(Convert.FromBase64String(clientKeyStr)).ToByteArray();
            byte[] blobBytes = Convert.FromBase64String(blobStr);

            byte[] iv = new byte[16];
            Array.Copy(blobBytes, 0, iv, 0, 16);

            byte[] encrypted = new byte[blobBytes.Length - 16 - 20];
            Array.Copy(blobBytes, 16, encrypted, 0, encrypted.Length);

            byte[] checksum = new byte[20];
            Array.Copy(blobBytes, blobBytes.Length - 20, checksum, 0, 20);
            
            Sha1Digest sha1 = new Sha1Digest();
            sha1.BlockUpdate(sharedKey, 0, sharedKey.Length);
            byte[] shaOutput = new byte[sha1.GetDigestSize()];
            sha1.DoFinal(shaOutput, 0);
            byte[] baseKey = new byte[16];
            Array.Copy(shaOutput, 0, baseKey, 0, 16);

            HMac hmac = new HMac(new Sha1Digest());
            hmac.Init(new KeyParameter(baseKey));
            byte[] tmp = Encoding.UTF8.GetBytes("checksum");
            hmac.BlockUpdate(tmp, 0, tmp.Length);
            byte[] checksumKey = new byte[hmac.GetMacSize()];
            hmac.DoFinal(checksumKey, 0);

            hmac.Init(new KeyParameter(baseKey));
            tmp = Encoding.UTF8.GetBytes("encryption");
            hmac.BlockUpdate(tmp, 0, tmp.Length);
            byte[] encryptionKey = new byte[hmac.GetMacSize()];
            hmac.DoFinal(encryptionKey, 0);

            hmac.Init(new KeyParameter(checksumKey));
            hmac.BlockUpdate(encrypted, 0, encrypted.Length);
            byte[] mac = new byte[hmac.GetMacSize()];
            hmac.DoFinal(mac, 0);

            if (!CompareArrays(mac, checksum))
            {
                LOGGER.Error("Mac and checksum don't match!");
                response.StatusCode = HttpStatusCode.BadRequest;
                return;
            }

            IBufferedCipher aes = new BufferedBlockCipher(new SicBlockCipher(new AesEngine()));
            aes.Init(false, new ParametersWithIV(new KeyParameter(SubArray(encryptionKey, 0, 16)), iv));
            byte[] decrypted = aes.DoFinal(encrypted);

            try
            {
                CloseSession();
            }
            catch (IOException ex)
            {
                LOGGER.Warn("Failed closing previous session.", ex);
            }

            try
            {
                lock (_connectionLock)
                {
                    _connectingUsername = userName;
                }

                LOGGER.InfoFormat("Accepted new user from {0}. deviceId: {1}", parameters["deviceName"], _inner._deviceId);

                // Sending response
                string resp = DEFAULT_SUCCESSFUL_ADD_USER.ToString();
                response.StatusCode = HttpStatusCode.OK;
                response.Content = resp;
                
                _session = new Session.Builder(_inner._conf)
                    .SetDeviceId(_inner._deviceId)
                    .SetDeviceName(_inner._deviceName)
                    .SetDeviceType(_inner._deviceType)
                    .SetPreferredLocale(_inner._preferredLocale)
                    .Blob(userName, decrypted)
                    .Create();

                lock (_connectionLock)
                {
                    _connectingUsername = null;
                }

                foreach (var listener in _sessionListeners)
                    listener.SessionChanged(_session);
            }
            catch (Exception ex) when (ex is Session.SpotifyAuthenticationException ||
                                       ex is MercuryClient.MercuryException ||
                                       ex is IOException || ex is GeneralSecurityException)
            {
                LOGGER.Error("Couldn't establish a new session.", ex);

                lock (_connectionLock)
                {
                    _connectingUsername = null;
                }
                
                response.StatusCode = HttpStatusCode.InternalServerError;
            }
        }
        
        private static byte[] SubArray(byte[] data, int index, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private static bool CompareArrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public void AddSessionListener(SessionListener listener)
        {
            _sessionListeners.Add(listener);
        }

        public void RemoveSessionListener(SessionListener listener)
        {
            _sessionListeners.Remove(listener);
        }

        public interface SessionListener
        {
            /// <summary>
            /// The session instance is going to be closed after this call.
            /// </summary>
            /// <param name="session">The old <see cref="Session"/></param>
            void SessionClosing(Session session);

            /// <summary>
            /// The session instance changed. <see cref="SessionClosing"/> has ben already called.
            /// </summary>
            /// <param name="session">The new <see cref="Session"/></param>
            void SessionChanged(Session session);
        }
    }

    public class ZeroconfBuilder : Session.AbsBuilder<ZeroconfBuilder>
    {
        private bool _listenAll = true;
        private int _listenPort = -1;
        private String[] _listenInterfaces = null;

        public ZeroconfBuilder(Session.Configuration conf) : base(conf)
        {
        }

        public ZeroconfBuilder()
        {
        }

        protected override ZeroconfBuilder This()
        {
            return this;
        }

        public ZeroconfBuilder SetListenAll(bool listenAll)
        {
            _listenAll = listenAll;
            _listenInterfaces = null;
            return this;
        }

        public ZeroconfBuilder SetListenPort(int listenPort)
        {
            _listenPort = listenPort;
            return this;
        }

        public ZeroconfBuilder SetListenInterfaces(String[] listenInterfaces)
        {
            _listenAll = false;
            _listenInterfaces = listenInterfaces;
            return this;
        }

        public ZeroconfServer Create()
        {
            return new ZeroconfServer(new Inner(
                DeviceType,
                DeviceName,
                DeviceId,
                PreferredLocale,
                Conf
            ), _listenPort, _listenAll, _listenInterfaces);
        }
    }

    public class Inner
    {
        internal readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();
        internal readonly DeviceType _deviceType;
        internal String _deviceName;
        internal String _deviceId;
        internal String _preferredLocale;
        internal Session.Configuration _conf;

        internal Inner(DeviceType deviceType, String deviceName, String deviceId, String preferredLocale,
            Session.Configuration conf)
        {
            _deviceType = deviceType;
            _deviceName = deviceName;
            _deviceId = deviceId;
            _preferredLocale = preferredLocale;
            _conf = conf;
        }
    }

    public class HttpRunner : IHttpServerEndpointHandler, IDisposable
    {
        private readonly HttpServer _server;
        private readonly int _port;
        private ILog LOGGER = LogManager.GetLogger(typeof(HttpRunner));
        private ZeroconfServer _zeroconfServer;
        
        public HttpRunner(int port, ZeroconfServer zeroconfServer)
        {
            _server = new HttpServer();
            _server.RegisterHandler(this);
            _port = port;
            _zeroconfServer = zeroconfServer;
        }
        
        public HttpServerResponse HandleRequest(HttpListenerContext context, Dictionary<String, String> _)
        {
            HttpListenerRequest request = context.Request;
            String method = request.HttpMethod;
            String path = request.Url.AbsolutePath;
            HttpServerResponse response = new HttpServerResponse();
            
            if (!_zeroconfServer.HasValidSession())
                LOGGER.DebugFormat("Handling request: {0} {2}, headers: {3}", method, path, request.Headers);

            Dictionary<String, String> parameters = new Dictionary<string, string>();
            if (method.Equals("POST"))
            {
                String contentType = request.Headers.Get("Content-Type");
                if (!Equals(contentType, "application/x-www-form-urlencoded"))
                {
                    LOGGER.ErrorFormat("Bad Content-Type {0}", contentType);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    return response;
                }

                if (!request.HasEntityBody)
                {
                    LOGGER.Error("Post request without body");
                    response.StatusCode = HttpStatusCode.BadRequest;
                    return response;
                }
                
                MemoryStream dataStream = new MemoryStream();
                byte[] buffer = new Byte[2048];
                int length;
                while ((length = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                    dataStream.Write(buffer, 0, length);
                
                byte[] body =  dataStream.ToArray();
                dataStream.Dispose();

                var query = HttpUtility.ParseQueryString(Encoding.UTF8.GetString(body));
                foreach (var key in query.AllKeys)
                    parameters[key] = query[key];
            }
            else
            {
                foreach(var key in request.QueryString.AllKeys) 
                    parameters[key] = request.QueryString[key];
            }

            parameters.TryGetValue("action", out var action);
            if (action == null)
            {
                LOGGER.Error("Request is missing action.");
                response.StatusCode = HttpStatusCode.BadRequest;
                return response;
            }

            if (Equals(action, "addUser"))
            {
                _zeroconfServer.HandleAddUser(response, parameters);
            } else if (Equals(action, "getInfo"))
            {
                _zeroconfServer.HandleGetInfo(response);
            }
            else
            {
                LOGGER.WarnFormat("Unknown action: {0}", action);
                response.StatusCode = HttpStatusCode.BadRequest;
            }

            return response;
        }

        public void Start()
        {
            _server.Start("0.0.0.0", _port);
            LOGGER.InfoFormat("Zeroconf HTTP server started successfully on port {0}!", _port);
        }

        public void Dispose()
        {
            _server.Stop();
        }

        public string Path { get => "/"; }
        public string Method { get => "*"; }
    }
}