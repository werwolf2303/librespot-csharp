using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EasyHttp.Http;
using log4net;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Security;
using Spotify;

namespace lib.core
{
    public class OAuth : IDisposable
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(OAuth));
        private static String SPOTIFY_AUTH =
            "https://accounts.spotify.com/authorize?response_type=code&client_id={0}&redirect_uri={1}&code_challenge={2}&code_challenge_method=S256&scope={3}";
        private static String[] SCOPES = {"app-remote-control", "playlist-modify", "playlist-modify-private", "playlist-modify-public", "playlist-read", "playlist-read-collaborative", "playlist-read-private", "streaming", "ugc-image-upload", "user-follow-modify", "user-follow-read", "user-library-modify", "user-library-read", "user-modify", "user-modify-playback-state", "user-modify-private", "user-personalized", "user-read-birthdate", "user-read-currently-playing", "user-read-email", "user-read-play-history", "user-read-playback-position", "user-read-playback-state", "user-read-private", "user-read-recently-played", "user-top-read"};
        private static String SPOTIFY_TOKEN = "https://accounts.spotify.com/api/token";
        private static String SPOTIFY_TOKEN_DATA = "grant_type=authorization_code&client_id={0}&redirect_uri={1}&code={2}&code_verifier={3}";

        private String clientId;
        private Uri redirectUrl;
        private SecureRandom random = new SecureRandom();
        private Object credentialsLock = new Object();

        private String codeVerifier;
        private String code;
        private String token;
        private HttpClient client = new HttpClient();
        
        private volatile HttpListener listener;
        private Thread serverThread;
        private volatile bool serverAlive;

        private String successUrl = "https://open.spotify.com/desktop/auth/success";
        private String failureUrl = "https://open.spotify.com/desktop/auth/error";
        
        public OAuth(String clientId, Uri redirectUrl)
        {
            this.clientId = clientId;
            this.redirectUrl = redirectUrl;
        }

        private String generateCodeVerifier()
        {
            String possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 128; i++)
            {
                sb.Append(possible[random.Next(possible.Length)]);
            }
            return sb.ToString();
        }

        private String generateCodeChallenge(String codeVerifier)
        {
            byte[] hashed = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(hashed).Replace("=", "").Replace("+", "-").Replace("/", "_");
        }

        public String getAuthUrl()
        {
            codeVerifier = generateCodeVerifier();
            return String.Format(SPOTIFY_AUTH, clientId, redirectUrl, generateCodeChallenge(codeVerifier), String.Join("+", SCOPES));
        }

        public void setCode(String code)
        {
            this.code = code;
        }

        public void requestToken()
        {
            if (code == null)
            {
                throw new Exception("Illegal state! You need to provide a code before!");
            }

            client.Request.Accept = "application/json";
            HttpResponse response = client.Post(
                SPOTIFY_TOKEN,
                String.Format(SPOTIFY_TOKEN_DATA, clientId, redirectUrl, code, codeVerifier),
                "application/x-www-form-urlencoded"
            );
            client.Request.Accept = "";
            JObject obj = JObject.Parse(response.RawText);
            token = obj["access_token"].ToString();
        }

        public LoginCredentials getCredentials()
        {
            if (token == null)
            {
                throw new Exception("Illegal state! You need to request a token before!");
            }

            return new LoginCredentials
            {
                Typ = AuthenticationType.AuthenticationSpotifyToken,
                AuthData = Encoding.UTF8.GetBytes(token)
            };
        }

        private void serverThreadFunc()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(redirectUrl.Scheme + "://" + redirectUrl.Host + ":" + redirectUrl.Port + "/");
            listener.Start();
            while (serverAlive)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                
                if (request.RawUrl.StartsWith(redirectUrl.AbsolutePath))
                {
                    byte[] data;
                    String templateHTML =
                        "<script>window.location.href='{0}';</script>";
                    bool ok = false;
                    
                    String code = request.QueryString["code"];
                    if (code != null)
                    {
                        data = Encoding.UTF8.GetBytes(String.Format(templateHTML, successUrl));
                        setCode(code);
                        ok = true;
                    }
                    else data = Encoding.UTF8.GetBytes(String.Format(templateHTML, failureUrl));
                    
                    response.ContentType = "text/html";
                    response.ContentEncoding = Encoding.UTF8;
                    response.ContentLength64 = data.Length;
                    response.OutputStream.Write(data, 0, data.Length);
                    response.Close();

                    if (ok)
                    {
                        Dispose();
                        lock (credentialsLock)
                        {
                            Monitor.PulseAll(credentialsLock);
                        }
                    }
                }
            }
        }

        public void runCallbackServer()
        {
            serverAlive = true;
            serverThread = new Thread(serverThreadFunc);
            serverThread.Name = "OAuth Server Thread";
            serverThread.Start();
            LOGGER.Info("OAuth: Waiting for callback on " + redirectUrl.Scheme + "://" + redirectUrl.Host + ":" + redirectUrl.Port + "/");
        }

        public LoginCredentials flow()
        {
            LOGGER.Info("OAuth: Visit in your browser and log in: " + getAuthUrl());
            runCallbackServer();
            lock (credentialsLock)
            {
                Monitor.Wait(credentialsLock);
            }
            requestToken();
            return getCredentials();
        }
        
        public void Dispose()
        {
            if (listener != null)
            {
                serverAlive = false;
                listener.Stop();
            }
        }
    }
}