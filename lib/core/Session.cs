using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Sockets;
using Connectstate;
using lib.common;
using lib.crypto;
using log4net;

namespace lib.core
{
    public class Session
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

        // private ScheduledExecutorService scheduler
        private bool authLock = false;
        private HttpClient client;
        private List<CloseListener> closeListeners = new List<CloseListener>();
        private List<ReconnectionListener> reconnectionListeners = new List<ReconnectionListener>();
        private Dictionary<String, String> userAttributtes = new Dictionary<String, String>();
        private ConnectionHolder conn;
        private volatile CipherPair cipherPair;

        public Random random()
        {
            return inner.random;
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
            DeviceType deviceType;
            String deviceName;
            public Random random;
            String deviceId;
            Session.Configuration conf;
            String preferredLocale;

            Inner(DeviceType deviceType, String deviceName, String deviceId, String preferredLocale, Configuration conf)
            {
                random = new Random();
                this.preferredLocale = preferredLocale;
                this.conf = conf;
                this.deviceType = deviceType;
                this.deviceName = deviceName;
                this.deviceId = String.IsNullOrEmpty(deviceId) ? Utils.randomHexString(random, 40).ToLower() : deviceId;
            }
        }

        public class Configuration
        {
            // Proxy
            //public bool proxyEnabled;
            //public Proxy.Type proxyType;
            //public bool proxySSL;
            //public String proxyAddress;
            //public int proxyPort;
            //public bool proxyAuth;
            //public String proxyUsername;
            //public String proxyPassword;

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

            /*private Configuration(bool proxyEnabled, Proxy.Type proxyType, bool proxySSL, String proxyAddress,
                                  int proxyPort, bool proxyAuth, String proxyUsername, String proxyPassword,
                                  TimeProvider.Method timeSynchronizationMethod, int timeManualCorrection,
                                  bool cacheEnabled, File cacheDir, bool doCacheCleanUp,
                                  bool storeCredentials, File storedCredentialsFile,
                                  bool retryOnChunkError, int connectionTimeout) {
                this.proxyEnabled = proxyEnabled;
                this.proxyType = proxyType;
                this.proxySSL = proxySSL;
                this.proxyAddress = proxyAddress;
                this.proxyPort = proxyPort;
                this.proxyAuth = proxyAuth;
                this.proxyUsername = proxyUsername;
                this.proxyPassword = proxyPassword;
                this.timeSynchronizationMethod = timeSynchronizationMethod;
                this.timeManualCorrection = timeManualCorrection;
                this.cacheEnabled = cacheEnabled;
                this.cacheDir = cacheDir;
                this.doCacheCleanUp = doCacheCleanUp;
                this.storeCredentials = storeCredentials;
                this.storedCredentialsFile = storedCredentialsFile;
                this.retryOnChunkError = retryOnChunkError;
                this.connectionTimeout = connectionTimeout;
            }*/

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
                // Proxy
                //private bool proxyEnabled = false;
                //private Proxy.Type proxyType;
                //private bool proxySSL = false;
                //private String proxyAddress;
                //private int proxyPort;
                //private bool proxyAuth;
                //private String proxyUsername;
                //private String proxyPassword;

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

                /*
                public Builder setProxyEnabled(bool proxyEnabled) {
                    this.proxyEnabled = proxyEnabled;
                    return this;
                }

                public Builder setProxyType(Proxy.Type proxyType) {
                    this.proxyType = proxyType;
                    return this;
                }

                public Builder setProxySSL(bool proxySSL) {
                    this.proxySSL = proxySSL;
                    return this;
                }

                public Builder setProxyAddress(String proxyAddress) {
                    this.proxyAddress = proxyAddress;
                    return this;
                }

                public Builder setProxyPort(int proxyPort) {
                    this.proxyPort = proxyPort;
                    return this;
                }

                public Builder setProxyAuth(bool proxyAuth) {
                    this.proxyAuth = proxyAuth;
                    return this;
                }

                public Builder setProxyUsername(String proxyUsername) {
                    this.proxyUsername = proxyUsername;
                    return this;
                }

                public Builder setProxyPassword(String proxyPassword) {
                    this.proxyPassword = proxyPassword;
                    return this;
                }
                */

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

                /*public Configuration build() {
                    return new Configuration(proxyEnabled, proxyType, proxySSL, proxyAddress, proxyPort, proxyAuth,
                            proxyUsername, proxyPassword,
                            timeSynchronizationMethod, timeManualCorrection,
                            cacheEnabled, cacheDir, doCacheCleanUp,
                            storeCredentials, storedCredentialsFile,
                            retryOnChunkError, connectionTimeout);
                }*/

                public Configuration build()
                {
                    return new Configuration(timeSynchronizationMethod, timeManualCorrection,
                        cacheEnabled, cacheDir, doCacheCleanUp,
                        storeCredentials, storedCredentialsFile,
                        retryOnChunkError, connectionTimeout);
                }
            }
        }

        private class ConnectionHolder
        {
            TcpClient client;

            /// <exception cref="IOException"></exception>
            private ConnectionHolder(TcpClient client)
            {
                this.client = client;
            }

            /// <exception cref="IOException"></exception>
            static ConnectionHolder create(String addr, Configuration conf)
            {
                String[] split = addr.Split(':');
                String apAddr = split[0];
                int apPort = int.Parse(split[1]);
                return new ConnectionHolder(new TcpClient(apAddr, apPort));
                /*if (!conf.proxyEnabled || conf.proxyType == Proxy.Type.DIRECT)
                    return new ConnectionHolder(new Socket(apAddr, apPort));

                switch (conf.proxyType) {
                    case HTTP:
                        Socket sock;
                        if (conf.proxySSL) {
                            sock = SSLSocketFactory.getDefault().createSocket(conf.proxyAddress, conf.proxyPort);
                        } else {
                            sock = new Socket(conf.proxyAddress, conf.proxyPort);
                        }
                        OutputStream out = sock.getOutputStream();
                        DataInputStream in = new DataInputStream(sock.getInputStream());

                        out.write(String.format("CONNECT %s:%d HTTP/1.0\n", apAddr, apPort).getBytes());
                        if (conf.proxyAuth)
                            out.write(String.format("Proxy-Authorization: %s\n", Credentials.basic(conf.proxyUsername, conf.proxyPassword)).getBytes());

                        out.write('\n');
                        out.flush();

                        String sl = Utils.readLine(in);
                        if (!sl.contains("200")) throw new IOException("Failed connecting: " + sl);

                        //noinspection StatementWithEmptyBody
                        while (!Utils.readLine(in).isEmpty()) {
                            // Read all headers
                        }

                        LOGGER.info(String.format("Successfully connected to the %s proxy.", conf.proxySSL ? "HTTPS" : "HTTP"));
                        return new ConnectionHolder(sock);
                    case SOCKS:
                        if (conf.proxyAuth) {
                            java.net.Authenticator.setDefault(new java.net.Authenticator() {
                                final String username = conf.proxyUsername;
                                final String password = conf.proxyPassword;

                                @Override
                                protected PasswordAuthentication getPasswordAuthentication() {
                                    if (Objects.equals(getRequestingProtocol(), "SOCKS5") && Objects.equals(getRequestingPrompt(), "SOCKS authentication"))
                                        return new PasswordAuthentication(username, password.toCharArray());

                                    return super.getPasswordAuthentication();
                                }
                            });
                        }

                        Proxy proxy = new Proxy(conf.proxyType, new InetSocketAddress(conf.proxyAddress, conf.proxyPort));
                        Socket proxySocket = new Socket(proxy);
                        proxySocket.connect(new InetSocketAddress(apAddr, apPort));
                        LOGGER.info("Successfully connected to the SOCKS proxy.");
                        return new ConnectionHolder(proxySocket);
                    default:
                        throw new UnsupportedOperationException();
                } */
            }
        }
    }
}