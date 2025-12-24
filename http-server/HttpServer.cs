using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using log4net;

namespace api.server
{
    public class HttpServer
    {
        private List<IHttpServerEndpointHandler> _endpoints = new List<IHttpServerEndpointHandler>();
        private volatile bool _serverRunning = false;
        private Thread _serverThread;
        private List<Thread> _connections = new List<Thread>();
        private String _host;
        private int _port;
        private int _connectionLimit = 5;
        private ILog LOGGER = LogManager.GetLogger(typeof(HttpServer));
        private object _connectionLock = new object();
        private String _corsPath = null;
        private bool _connectionLimitReached = false;
        private object _requestLimitLock = new object();
        
        public HttpServer RegisterHandler(IHttpServerEndpointHandler endpoint)
        {
            _endpoints.Add(endpoint);
            return this;
        }

        public HttpServer RegisterHandlers(List<IHttpServerEndpointHandler> endpoints)
        {
            _endpoints.AddRange(endpoints);
            return this;
        }

        private void Run()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://" + _host + ":" + _port + "/");
            listener.Start();
            
            LOGGER.InfoFormat("Server started on {0}:{1} !", _host, _port);
            
            while (_serverRunning)
            {
                if (_connectionLimitReached) 
                    lock (_requestLimitLock)
                        Monitor.Wait(_requestLimitLock);
                HttpListenerContext context = listener.GetContext();
                lock (_connectionLock)
                {
                    Thread connectionThread = new Thread(() => HandleConnection(context));
                    connectionThread.Start();
                    _connections.Add(connectionThread);
                    
                    if (_connections.Count >= _connectionLimit)
                    {
                        _connectionLimitReached = true;
                    }
                }
            }
        }

        private void HandleConnection(HttpListenerContext context)
        {
            if (!_serverRunning)
            {
                if (_corsPath != null) 
                    context.Response.AppendHeader("Access-Control-Allow-Origin", _corsPath);
                new HttpServerResponse(
                    statusCode: HttpStatusCode.ServiceUnavailable, 
                    content: "Server was shut down"
                ).WriteResponse(context);
                return;
            }
            
            bool requestHandled = false;
            foreach (var endpoint in _endpoints)
            {
                if (endpoint.Method != "*" && !new Regex(endpoint.Method).Match(context.Request.HttpMethod).Success) continue;
                var matcher = new PathTemplateMatcher(endpoint.Path);
                Dictionary<String, String> pathParams = null;
                if (endpoint.Path.Equals("/") || matcher.Match(context.Request.Url.AbsolutePath, out pathParams))
                {
                    requestHandled = true;
                    
                    if (_corsPath != null) 
                        context.Response.AppendHeader("Access-Control-Allow-Origin", _corsPath);
                    endpoint.HandleRequest(context, pathParams).WriteResponse(context);
                }
            }
            
            LOGGER.DebugFormat(
                "Incoming request! IP: '{0}' Path: '{1}' Was Handled: {2}",
                context.Request.UserHostAddress.Split(':')[0],
                context.Request.Url.AbsolutePath,
                requestHandled
            );
            
            CleanupConnections();
        }

        public HttpServer EnableCors(String allowedOrigins = "*")
        {
            _corsPath = allowedOrigins;
            return this;
        }

        private void CleanupConnections()
        {
            lock (_connectionLock)
            {
                int connectionsRemoved = _connections.RemoveAll(x => !x.IsAlive);
                if (connectionsRemoved > 0)
                {
                    lock (_requestLimitLock)
                    {
                        _connectionLimitReached = false;
                        Monitor.PulseAll(_requestLimitLock);
                    }
                }
            }
        }
        
        public HttpServer SetMaxConnections(int maxConnections)
        {
            _connectionLimit = maxConnections;
            return this;
        }

        public HttpServer Start(String host, int port)
        {
            if (_serverThread != null) throw new InvalidOperationException("Already running");
            _host = host;
            _port = port;
            _serverRunning = true;
            _serverThread = new Thread(Run);
            _serverThread.IsBackground = true;
            _serverThread.Start();
            return this;
        }

        public HttpServer Stop()
        {
            _serverRunning = false;
            lock (_connectionLock)
                foreach (var connection in _connections)
                    connection.Join(3000);
            _serverThread.Join(1500);
            _serverThread = null;
            return this;
        }
    }

    public class HttpServerEndpoint : IHttpServerEndpointHandler
    {
        private readonly Handler _handler;
            
        public HttpServerEndpoint(Handler handler, String path = "/", String method = "GET")
        {
            _handler = handler;
            Path = path;
            Method = method;
        }
        
        public delegate HttpServerResponse Handler(HttpListenerContext context, Dictionary<String, String> pathParams);

        public HttpServerResponse HandleRequest(HttpListenerContext context, Dictionary<String, String> pathParams)
        {
            return _handler(context, pathParams);
        }

        public string Path
        {
            get;
            set;
        }

        public string Method
        {
            get;
            set;
        }
    }

    public interface IHttpServerEndpointHandler
    {
        HttpServerResponse HandleRequest(HttpListenerContext context, Dictionary<String, String> pathParameters);
        
        String Path { get; }
        
        String Method { get; }
    }

    public class HttpServerResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public String Content { get; set; }
        public Encoding Encoding { get; set; }
        public String ContentType { get; set; }
        public WebHeaderCollection Headers { get; set; }

        public HttpServerResponse(HttpStatusCode statusCode = HttpStatusCode.OK, String content = "", String contentType = "text/html", Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            
            StatusCode = statusCode;
            Content = content;
            Encoding = encoding;
            ContentType = contentType;
        }

        public void WriteResponse(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            response.StatusCode = (int) StatusCode;
            response.ContentEncoding = Encoding;
            response.ContentType = ContentType;
            
            if (Headers != null)
                foreach (string key in Headers.AllKeys)
                    response.Headers[key] = Headers[key];
            
            byte[] data = Encoding.GetBytes(Content);
            response.ContentLength64 = data.Length;
            
            response.OutputStream.Write(data, 0, data.Length);
            response.Close();
        }
    }
    
    public sealed class PathTemplateMatcher
    {
        public string Template { get; private set; }
        public int SegmentCount { get; private set; }
        public int StaticSegmentCount { get; private set; }

        private readonly Regex _regex;
        private readonly List<string> _paramNames = new List<string>();

        public PathTemplateMatcher(string template)
        {
            if (string.IsNullOrEmpty(template))
                throw new ArgumentException("Template cannot be null or empty");

            Template = template;

            string[] segments = template.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            SegmentCount = segments.Length;

            int staticCount = 0;
            foreach (string s in segments)
            {
                if (!IsParameterSegment(s))
                    staticCount++;
            }
            StaticSegmentCount = staticCount;

            string pattern = "^" + Regex.Replace(
                template,
                @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}",
                m =>
                {
                    string name = m.Groups[1].Value;
                    _paramNames.Add(name);
                    return "(?<" + name + ">[^/]+)";
                }) + "$";

            _regex = new Regex(pattern, RegexOptions.Compiled);
        }

        public bool Match(string path, out Dictionary<string, string> parameters)
        {
            parameters = null;
            if (path == null)
                return false;

            Match match = _regex.Match(path);
            if (!match.Success)
                return false;

            parameters = new Dictionary<string, string>(_paramNames.Count);
            foreach (string name in _paramNames)
                parameters[name] = match.Groups[name].Value;

            return true;
        }

        private static bool IsParameterSegment(string segment)
        {
            return segment.Length > 2 &&
                   segment[0] == '{' &&
                   segment[segment.Length - 1] == '}';
        }
    }
}