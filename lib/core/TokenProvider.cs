using System;
using System.IO;
using lib.common;
using lib.json;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using spotify.login5.v3;
using spotify.login5.v3.credentials;

namespace lib.core
{
    public class TokenProvider
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(TokenProvider));
        private static int TOKEN_EXPIRE_THRESHOLD = 10;
        private Session _session;
        private StoredToken _token;
        
        internal TokenProvider(Session session)
        {
            _session = session;
        }
        
        public StoredToken GetToken()
        {
            lock (this)
            {
                if (_token != null)
                {
                    if (_token.Expired()) _token = null;
                    return _token;
                }
                
                LOGGER.DebugFormat("Token expired or not suitable, requesting again. (oldToken: {0})", _token);

                Login5Api api = new Login5Api(_session);
                LoginResponse resp = api.Login5(new LoginRequest
                {
                    StoredCredential = new StoredCredential
                    {
                        Username = _session.Username(),
                        Data = _session.GetAPWelcome().ReusableAuthCredentials
                    }
                });
                if (resp.Ok == null) throw new IOException("Login5 returned an error: " + resp.Error);
                LoginOk ok = resp.Ok;

                _token = new StoredToken(ok.AccessToken, ok.AccessTokenExpiresIn);
                
                LOGGER.DebugFormat("Updated token successfully! (newToken: {0})", _token);

                return _token;
            }
        }

        public String Get()
        {
            return GetToken().AccessToken;
        }

        public class StoredToken
        {
            public int ExpiresIn;
            public String AccessToken;
            public long Timestamp;

            internal StoredToken(String accessToken, int expiresIn)
            {
                Timestamp = Utils.getUnixTimeStampInMilliseconds();
                ExpiresIn = expiresIn;
                AccessToken = accessToken;
            }

            public bool Expired()
            {
                return Timestamp + (ExpiresIn - TOKEN_EXPIRE_THRESHOLD) * 1000L <
                       Utils.getUnixTimeStampInMilliseconds();
            }

            public override string ToString()
            {
                return String.Format("StoredToken(expiresIn={0}, accessToken={1}, timestamp={2})",
                    ExpiresIn, Utils.truncateMiddle(AccessToken, 12), Timestamp);
            }
        }
    }
}