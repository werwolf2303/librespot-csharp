using System;
using System.Collections.Generic;
using lib.common;
using lib.json;
using lib.mercury;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;

namespace lib.core
{
    public class TokenProvider
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(TokenProvider));
        private static int TOKEN_EXPIRE_THRESHOLD = 10;
        private Session _session;
        private List<StoredToken> _tokens = new List<StoredToken>();
        
        internal TokenProvider(Session session)
        {
            _session = session;
        }

        private StoredToken FindTokenWithAllScopes(String[] scopes)
        {
            foreach (StoredToken token in _tokens)
                if (token.HasScopes(scopes))
                    return token;

            return null;
        }

        public StoredToken GetToken(params string[] scopes)
        {
            lock (this)
            {
                if (scopes.Length == 0) throw new Exception("At least one scope must be specified");

                StoredToken token = FindTokenWithAllScopes(scopes);
                if (token != null)
                {
                    if (token.Expired()) _tokens.Remove(token);
                    return token;
                }
                
                LOGGER.DebugFormat("Token expired or not suitable, requesting agin. (scopes: {0}, oldToken: {1})", Arrays.ToString(scopes), token);
                GenericJson resp = _session.GetMercury()
                    .SendSync(MercuryRequests.RequestToken(_session.GetDeviceId(), String.Join(",", scopes)));
                token = new StoredToken(resp.Obj);
                
                LOGGER.DebugFormat("Updated token successfully! (scopes: {0}, newToken: {1})", Arrays.ToString(scopes), token);
                _tokens.Add(token);
                
                return token;
            }
        }

        public String Get(String scope)
        {
            return GetToken(scope).AccessToken;
        }

        public class StoredToken
        {
            public int ExpiresIn;
            public String AccessToken;
            public String[] Scopes;
            public long Timestamp;

            internal StoredToken(JObject obj)
            {
                Timestamp = Utils.getUnixTimeStampInMilliseconds();
                ExpiresIn = obj["expiresIn"].ToObject<int>();
                AccessToken = obj["accessToken"].ToObject<string>();
                
                JArray scopesArray = obj["scopes"] as JArray;
                Scopes = new String[scopesArray.Count];
                for (int i = 0; i < scopesArray.Count; i++)
                    Scopes[i] = scopesArray[i].ToObject<string>();
            }

            public bool Expired()
            {
                return Timestamp + (ExpiresIn - TOKEN_EXPIRE_THRESHOLD) * 1000L <
                       Utils.getUnixTimeStampInMilliseconds();
            }

            public override string ToString()
            {
                return String.Format("StoredToken(expiresIn={0}, accessToken={1}, scopes={2}, timestamp={3})",
                    ExpiresIn, Utils.truncateMiddle(AccessToken, 12), Arrays.ToString(Scopes), Timestamp);
            }

            public bool HasScope(String scope)
            {
                foreach (String s in Scopes)
                    if (s.Equals(scope))
                        return true;
                
                return false;
            }

            public bool HasScopes(String[] sc)
            {
                foreach (String s in sc)
                    if (!HasScope(s))
                        return false;

                return true;
            }
        }
    }
}