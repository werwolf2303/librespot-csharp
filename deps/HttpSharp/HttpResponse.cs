using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace deps.HttpSharp
{
    // https://en.wikipedia.org/wiki/List_of_HTTP_header_fields
    public class HttpResponse
    {
        public string AcceptCH { get; private set; }
        public string AccessControlAllowOrigin { get; private set; }
        public string AccessControlAllowCredentials { get; private set; }
        public string AccessControlExposeHeaders { get; private set; }
        public string AccessControlMaxAge { get; private set; }
        public string AccessControlAllowMethods { get; private set; }
        public string AccessControlAllowHeaders { get; private set; }
        public string AcceptPatch { get; private set; }
        public string AcceptRanges { get; private set; }
        public string Age { get; private set; }
        public string Allow { get; private set; }
        public string AltSvc { get; private set; }
        public string CacheControl { get; private set; }
        public string Connection { get; private set; }
        public string ContentDisposition { get; private set; }
        public string ContentLanguage { get; private set; }
        public string ContentLocation { get; private set; }

        /// <summary>
        /// Obsolete
        /// </summary>
        public string ContentMD5 { get; private set; }

        public string ContentRange { get; private set; }
        public string Date { get; private set; }
        public string DeltaBase { get; private set; }
        public string ETag { get; private set; }
        public string Expires { get; private set; }
        public string IM { get; private set; }
        public string Link { get; private set; }
        public string Location { get; private set; }
        public string P3P { get; private set; }
        public string Pragma { get; private set; }
        public string PreferenceApplied { get; private set; }
        public string ProxyAuthenticate { get; private set; }
        public string PublicKeyPins { get; private set; }
        public string RetryAfter { get; private set; }
        public string SetCookie { get; private set; }
        public string StrictTransportSecurity { get; private set; }
        public string Trailer { get; private set; }
        public string TransferEncoding { get; private set; }
        public string Tk { get; private set; }
        public string Upgrade { get; private set; }
        public string Vary { get; private set; }
        public string Via { get; private set; }

        /// <summary>
        /// Obsolete
        /// </summary>
        public string Warning { get; private set; }

        public string WWWAuthenticate { get; private set; }
        public string XFrameOptions { get; private set; }
        public string ContentSecurityPolicy { get; private set; }
        public string XContentSecurityPolicy { get; private set; }
        public string XWebKitCSP { get; private set; }
        public string ExpectCT { get; private set; }
        public string NEL { get; private set; }
        public string PermissionsPolicy { get; private set; }
        public string Refresh { get; private set; }
        public string ReportTo { get; private set; }
        public string Status { get; private set; }
        public string TimingAllowOrigin { get; private set; }
        public string XContentDuration { get; private set; }
        public string XPoweredBy { get; private set; }
        public string XRedirectBy { get; private set; }
        public string XRequestId { get; private set; }
        public string XCorrelationId { get; private set; }
        public string XUACompatible { get; private set; }
        public string XXSSProtection { get; private set; }

        public string CharacterSet
        {
            get => _httpWebResponse.CharacterSet;
        }

        public string ContentEncoding
        {
            get => _httpWebResponse.ContentEncoding;
        }

        public long ContentLength
        {
            get => _httpWebResponse.ContentLength;
        }

        public string ContentType
        {
            get => _httpWebResponse.ContentType;
        }

        public CookieCollection Cookies
        {
            get => _httpWebResponse.Cookies;
        }

        public bool IsMutuallyAuthenticated
        {
            get => _httpWebResponse.IsMutuallyAuthenticated;
        }

        public DateTime LastModified
        {
            get => _httpWebResponse.LastModified;
        }

        public Version ProtocolVersion
        {
            get => _httpWebResponse.ProtocolVersion;
        }

        public string Server
        {
            get => _httpWebResponse.Server;
        }

        public HttpStatusCode StatusCode
        {
            get => _httpWebResponse.StatusCode;
        }

        public string StatusDescription
        {
            get => _httpWebResponse.StatusDescription;
        }

        public bool SupportsHeaders
        {
            get => _httpWebResponse.SupportsHeaders;
        }

        public byte[] GetRequestData
        {
            get => _requestData;
        }

        public Dictionary<string, string> Headers = new Dictionary<string, string>();
        public HttpMethod HttpMethod { get; private set; }

        private byte[] _requestData;
        private Uri _uri;
        private HttpWebResponse _httpWebResponse;

        internal HttpResponse(HttpRequest request)
        {
            HttpMethod = request.HttpMethod;
            _requestData = request.RequestData;
            _httpWebResponse = (HttpWebResponse)request.GetRequest().GetResponse();

            AcceptCH = TryGet<string>("Accept-CH");
            AccessControlAllowOrigin = TryGet<string>("Access-Control-Allow-Origin");
            AccessControlAllowCredentials = TryGet<string>("Access-Control-Allow-Credentials");
            AccessControlExposeHeaders = TryGet<string>("Access-Control-Expose-Headers");
            AccessControlMaxAge = TryGet<string>("Access-Control-Max-Age");
            AccessControlAllowMethods = TryGet<string>("Access-Control-Allow-Methods");
            AccessControlAllowHeaders = TryGet<string>("Access-Control-Allow-Headers");
            AcceptPatch = TryGet<string>("Accept-Patch");
            AcceptRanges = TryGet<string>("Accept-Ranges");
            Age = TryGet<string>("Alt");
            Allow = TryGet<string>("Allow");
            AltSvc = TryGet<string>("Alt-Svc");
            CacheControl = TryGet<string>("Cache-Control");
            Connection = TryGet<string>("Connection");
            ContentDisposition = TryGet<string>("Content-Disposition");
            ContentLanguage = TryGet<string>("Content-Language");
            ContentLocation = TryGet<string>("Content-Location");
            ContentMD5 = TryGet<string>("Content-MD5");
            ContentRange = TryGet<string>("Content-Range");
            Date = TryGet<string>("Date");
            DeltaBase = TryGet<string>("Delta-Base");
            ETag = TryGet<string>("ETag");
            Expires = TryGet<string>("Expires");
            IM = TryGet<string>("IM");
            Link = TryGet<string>("Link");
            Location = TryGet<string>("Location");
            P3P = TryGet<string>("P3P");
            Pragma = TryGet<string>("Pragma");
            PreferenceApplied = TryGet<string>("Preference-Applied");
            ProxyAuthenticate = TryGet<string>("Proxy-Authenticate");
            PublicKeyPins = TryGet<string>("Public-Key-Pins");
            RetryAfter = TryGet<string>("Retry-After");
            SetCookie = TryGet<string>("Cookie");
            StrictTransportSecurity = TryGet<string>("Strict-Transport-Security");
            Trailer = TryGet<string>("Trailer");
            TransferEncoding = TryGet<string>("Transfer-Encoding");
            Tk = TryGet<string>("Tk");
            Upgrade = TryGet<string>("Upgrade");
            Vary = TryGet<string>("Vary");
            Via = TryGet<string>("Via");
            Warning = TryGet<string>("Warning");
            WWWAuthenticate = TryGet<string>("WWW-Authenticate");
            XFrameOptions = TryGet<string>("X-Frame-Options");
            ContentSecurityPolicy = TryGet<string>("Content-Security-Policy");
            XContentSecurityPolicy = TryGet<string>("X-Content-Security-Policy");
            XWebKitCSP = TryGet<string>("X-WebKit-CSP");
            ExpectCT = TryGet<string>("Expect-CT");
            NEL = TryGet<string>("NEL");
            PermissionsPolicy = TryGet<string>("Permissions-Policy");
            Refresh = TryGet<string>("Refresh");
            ReportTo = TryGet<string>("Report-To");
            Status = TryGet<string>("Status");
            TimingAllowOrigin = TryGet<string>("Timing-Allow-Origin");
            XContentDuration = TryGet<string>("X-Content-Duration");
            XPoweredBy = TryGet<string>("X-Powered-By");
            XRedirectBy = TryGet<string>("X-Redirect-By");
            XRequestId = TryGet<string>("X-Request-ID");
            XCorrelationId = TryGet<string>("X-Correlation-ID");
            XUACompatible = TryGet<string>("X-UA-Compatible");
            XXSSProtection = TryGet<string>("X-XSS-Protection");

            foreach (String key in _httpWebResponse.Headers.Keys)
            {
                Headers.Add(key, _httpWebResponse.Headers[key]);
            }
        }

        private W TryGet<W>(string key) where W : class
        {
            return _httpWebResponse.Headers.Get(key) as W;
        }

        public string GetResponseString()
        {
            return Encoding.UTF8.GetString(GetResponseBytes());
        }

        public byte[] GetResponseBytes()
        {
            if (Headers.ContainsKey("Content-Length"))
            {
                Stream cresponseStream = _httpWebResponse.GetResponseStream();
                byte[] cdata = new byte[(int)ContentLength];
                cresponseStream.Read(cdata, 0, (int)ContentLength);
                cresponseStream.Close();
                return cdata;
            }
            Stream responseStream = _httpWebResponse.GetResponseStream();
            MemoryStream dataStream = new MemoryStream();
            byte[] buffer = new Byte[2048];
            int length;
            while ((length = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                dataStream.Write(buffer, 0, length);
            return dataStream.ToArray();
        }

        public Stream GetResponseStream()
        {
            return _httpWebResponse.GetResponseStream();
        }
    }
}