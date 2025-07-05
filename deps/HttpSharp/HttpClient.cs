using System;
using System.Collections.Generic;
using System.Net;

namespace deps.HttpSharp
{
    public class HttpClient
    {
        public int Timeout { get; set; }
        public int ReadWriteTimeout { get; set; }
        public IWebProxy Proxy { get; set; }
        public HttpContinueDelegate ContinueDelegate { get; set; }
        private List<Intercept> _interceptors = new List<Intercept>();

        public HttpResponse NewCall(HttpRequest request)
        {
            request.Interceptors = _interceptors;
            if(Proxy != null) request.Proxy = Proxy;
            if(Timeout > 0) request.Timeout = Timeout;
            if(ReadWriteTimeout > 0) request.ReadWriteTimeout = ReadWriteTimeout;
            if(ContinueDelegate != null) request.ContinueDelegate = ContinueDelegate;
            return ProcessInterceptors(request);
        }

        private HttpResponse ProcessInterceptors(HttpRequest request)
        {
            foreach (Intercept interceptor in _interceptors)
            {
                if (!interceptor(request)) throw new RequestCancelledException("An Interceptor cancelled the request");
            }
            return new HttpResponse(request);
        }
        
        /// <summary>
        /// If false is returned, the request will be canceled
        /// </summary>
        public void AddInterceptor(Intercept interceptor)
        {
            _interceptors.Add(interceptor);
        }

        public void RemoveInterceptor(Intercept interceptor)
        {
            _interceptors.Remove(interceptor);
        }

        public class RequestCancelledException : Exception
        {
            public RequestCancelledException(string message) : base(message) { }
        }
        
        /// <summary>
        /// If false is returned, the request will be canceled
        /// </summary>
        public delegate bool Intercept(HttpRequest request);
    }
}