using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;

namespace deps.HttpSharp
{
    
    // https://en.wikipedia.org/wiki/List_of_HTTP_header_fields
    public class HttpRequest
    {
        public string AIM { get; set; }
        public string AcceptDatetime { get; set; }
        public string AccessControlRequestMethod { get; set; }
        public string AccessControlRequestHeaders { get; set; }
        public string Cookie { get; set; }
        public string Origin { get; set; }
        public string AcceptCharset { get; set; }
        public string AcceptEncoding { get; set; }
        public string AcceptLanguage { get; set; }
        public string Authorization { get; set; }
        public string CacheControl { get; set; }
        public string ContentEncoding { get; set; }
        public string Date { get; set; }
        public string Forwarded { get; set; }
        public string IfMatch { get; set; }
        public string IfNoneMatch { get; set; }
        public string IfRange { get; set; }
        public string IfUnmodifiedSince { get; set; }
        public string MaxForwards { get; set; }
        public string Prefer { get; set; }
        public string ProxyAuthorization { get; set; }
        public string TE { get; set; }
        public string Trailer { get; set; }
        public string Upgrade { get; set; }
        public string Via { get; set; }
        /// <summary>
        /// Outdated
        /// </summary>
        public string Pragma { get; set; }
        /// <summary>
        /// Obsolete
        /// </summary>
        public string ContentMd5 { get; set; }
        /// <summary>
        /// Obsolete
        /// </summary>
        public string HTTP2Settings { get; set; }
        /// <summary>
        /// Warning
        /// </summary>
        public string Warning { get; set; }
        public string UpgradeInsecureRequests { get; set; }
        public string XRequestedWith { get; set; }
        public string DNT { get; set; }
        public string XForwardedFor { get; set; }
        public string XForwardedHost { get; set; }
        public string XForwardedProto { get; set; }
        public string FrontEndHttps { get; set; }
        public string XHttpMethodOverride { get; set; }
        public string XAttDeviceId { get; set; }
        public string XWapProfile { get; set; }
        public string ProxyConnection { get; set; }
        public string XUIDH { get; set; }
        public string XCsrfToken { get; set; }
        public string XRequestId { get; set; }
        public string XCorrelationId { get; set; }
        public string CorrelationId { get; set; }
        public string SaveData { get; set; }
        public string SecGPC { get; set; }

        public RequestCachePolicy CachePolicy
        {
            get => _request.CachePolicy; set => _request.CachePolicy = value;
        }

        public ICredentials Credentials
        {
            get => _request.Credentials; set => _request.Credentials = value;
        }

        public IWebProxy Proxy
        {
            get => _request.Proxy; set => _request.Proxy = value; 
            
        }
        public string Address { get => _url.ToString(); }

        public DecompressionMethods AutomaticDecompression
        {
            get => _request.AutomaticDecompression; set => _request.AutomaticDecompression = value;
        }

        public bool Pipelined
        {
            get => _request.Pipelined; set => _request.Pipelined = value;
        }

        public HttpContinueDelegate ContinueDelegate
        {
            get => _request.ContinueDelegate; set => _request.ContinueDelegate = value; 
        }

        public int Timeout
        {
            get => _request.Timeout; set => _request.Timeout = value; 
        }

        public int ReadWriteTimeout
        {
            get => _request.ReadWriteTimeout; set => _request.ReadWriteTimeout = value;
        }

        public CookieContainer CookieContainer
        {
            get => _request.CookieContainer; set => _request.CookieContainer = value; 
        }

        public bool KeepAlive
        {
            get => _request.KeepAlive; set => _request.KeepAlive = value; 
        }

        public string MediaType
        {
            get => _request.MediaType; set => _request.MediaType = value; 
        }

        public bool PreAuthenticate
        {
            get; set; 
        }

        public Version ProtocolVersion
        {
            get => _request.ProtocolVersion; set => _request.ProtocolVersion = value;
        }

        public bool SendChunked
        {
            get => _request.SendChunked; set => _request.SendChunked = value; 
        }

        public bool UseDefaultCredentials
        {
            get => _request.UseDefaultCredentials; set => _request.UseDefaultCredentials = value; 
        }

        public long ContentLength
        {
            get => _request.ContentLength; set => _request.ContentLength = value; 
        }

        public string ContentType
        {
            get => _request.ContentType; set => _request.ContentType = value; 
        }

        public string Accept
        {
            get => _request.Accept; set => _request.Accept = value;
        }

        public bool AllowAutoRedirect
        {
            get => _request.AllowAutoRedirect; set => _request.AllowAutoRedirect = value;
        }

        public bool AllowWriteStreamBuffering
        {
            get => _request.AllowWriteStreamBuffering; set => _request.AllowWriteStreamBuffering = value; 
        }

        public string Connection
        {
            get => _request.Connection; set => _request.Connection = value;
        }

        public string ConnectionGroupName
        {
            get => _request.ConnectionGroupName; set => _request.ConnectionGroupName = value;
        }

        public string Expect
        {
            get => _request.Expect; set => _request.Expect = value;
        }

        public string Host
        {
            get => _request.Host; set => _request.Host = value;
        }

        public DateTime IfModifiedSince
        {
            get => _request.IfModifiedSince; set => _request.IfModifiedSince = value;
        }

        public int MaximumAutomaticRedirections
        {
            get => _request.MaximumAutomaticRedirections; set => _request.MaximumAutomaticRedirections = value;
        }

        public int MaximumResponseHeadersLength
        {
            get => _request.MaximumResponseHeadersLength; set => _request.MaximumResponseHeadersLength = value;
        }

        public string Referer
        {
            get => _request.Referer; set => _request.Referer = value;
        }

        public ServicePoint ServicePoint
        {
            get => _request.ServicePoint;
        }

        public string TransferEncoding
        {
            get => _request.TransferEncoding; set => _request.TransferEncoding = value;
        }

        public string UserAgent
        {
            get => _request.UserAgent; set => _request.UserAgent = value;
        }

        public bool UnsafeAuthenticatedConnectionSharing
        {
            get => _request.UnsafeAuthenticatedConnectionSharing; set => _request.UnsafeAuthenticatedConnectionSharing = value;
        }
        
        public Dictionary<string, string> ExtraHeaders = new Dictionary<string, string>();
        
        private Uri _url;
        internal HttpMethod HttpMethod;
        private Stream _responseStream;
        private HttpWebRequest _request;
        public byte[] RequestData;
        internal List<HttpClient.Intercept> Interceptors;
        internal int Tries = 0;
        
        public HttpRequest(Uri uri, HttpMethod httpMethod)
        {
            _url = uri;
            HttpMethod = httpMethod;
            _request = (HttpWebRequest) HttpWebRequest.Create(uri);
            _request.Method = HttpMethod.ToString().ToUpper();
            _request.KeepAlive = false;
        }
        
        public HttpRequest(string url, HttpMethod httpMethod)
        {
            _url = new Uri(url);
            HttpMethod = httpMethod;
            _request = (HttpWebRequest) HttpWebRequest.Create(url);
            _request.Method = HttpMethod.ToString().ToUpper();
            _request.KeepAlive = false;
        }

        public void AddRange(int range)
        {
            _request.AddRange(range);
        }

        public void AddRange(int from, int to)
        {
            _request.AddRange(from, to);
        }

        public void AddRange(string rangeSpecifier, int range)
        {
            _request.AddRange(rangeSpecifier, range);
        }

        public void AddRange(string rangeSpecifier, int from, int to)
        {
            _request.AddRange(rangeSpecifier, from, to);
        }

        public void AddRange(long range)
        {
            _request.AddRange(range);
        }

        public void AddRange(long from, long to)
        {
            _request.AddRange(from, to);
        }

        public void AddRange(string rangeSpecifier, long range)
        {
            _request.AddRange(rangeSpecifier, range);
        }

        public void SetData(byte[] data)
        {
            RequestData = data;
            ContentLength = data.Length;
        }

        public void SetData(String data)
        {
            SetData(Encoding.UTF8.GetBytes(data));
        }

        public void SetData(MemoryStream data)
        {
            byte[] buffer = new byte[data.Length];
            if (data.CanSeek) data.Seek(0, SeekOrigin.Begin);
            data.Read(buffer, 0, buffer.Length);
            SetData(buffer);
        }

        private void AddHeader(string key, string value)
        {
            if (value == null) return;
            _request.Headers.Add(key, value);
        }

        internal HttpWebRequest GetRequest()
        {
            AddHeader("A-IM", AIM);
            AddHeader("Accept-Datetime", AcceptDatetime);
            AddHeader("Access-Control-Request-Method", AccessControlRequestMethod);
            AddHeader("Access-Control-Request-Headers", AccessControlRequestHeaders);
            AddHeader("Cookie", Cookie);
            AddHeader("Origin", Origin);
            AddHeader("Accept-Charset", AcceptCharset);
            AddHeader("Accept-Encoding", AcceptEncoding);
            AddHeader("Accept-Language", AcceptLanguage);
            AddHeader("Authorization", Authorization);
            AddHeader("Cache-Control", CacheControl);
            AddHeader("Content-Encoding", ContentEncoding);
            AddHeader("Date", Date);
            AddHeader("Forwarded", Forwarded);
            AddHeader("If-Match", IfMatch);
            AddHeader("If-None-Match", IfNoneMatch);
            AddHeader("If-Range", IfRange);
            AddHeader("If-Unmodified-Since", IfUnmodifiedSince);
            AddHeader("Max-Forwards", MaxForwards);
            AddHeader("Prefer", Prefer);
            AddHeader("Proxy-Authorization", ProxyAuthorization);
            AddHeader("TE", TE);
            AddHeader("Trailer", Trailer);
            AddHeader("Upgrade", Upgrade);
            AddHeader("Via", Via);
            AddHeader("Pragma", Pragma);
            AddHeader("Content-MD5", ContentMd5);
            AddHeader("HTTP2-Settings", HTTP2Settings);
            AddHeader("Warning", Warning);
            AddHeader("Upgrade-Insecure-Requests", UpgradeInsecureRequests);
            AddHeader("X-Requested-With", XRequestedWith);
            AddHeader("DNT", DNT);
            AddHeader("X-Forwarded-For", XForwardedFor);
            AddHeader("X-Forwarded-Host", XForwardedHost);
            AddHeader("X-Forwarded-Proto", XForwardedProto);
            AddHeader("Front-End-Https", FrontEndHttps);
            AddHeader("X-Http-Method-Override", XHttpMethodOverride);
            AddHeader("X-ATT-DeviceId", XAttDeviceId);
            AddHeader("X-Wap-Profile", XWapProfile);
            AddHeader("Proxy-Connetion", ProxyConnection);
            AddHeader("X-UIDH", XUIDH);
            AddHeader("X-Csrf-Token", XCsrfToken);
            AddHeader("X-Request-ID", XRequestId);
            AddHeader("X-Correlation-ID", XCorrelationId);
            AddHeader("Correlation-ID", CorrelationId);
            AddHeader("Save-Data", SaveData);
            AddHeader("Sec-GPC", SecGPC);

            foreach (KeyValuePair<String, String> pair in ExtraHeaders)
                AddHeader(pair.Key, pair.Value);

            return _request;
        }
    }
}