using System;
using System.Collections.Generic;
using System.Net;
using api.server;
using lib.core;

namespace api.handlers
{
    public abstract class AbsSessionHandler : IHttpServerEndpointHandler
    {
        private SessionWrapper _sessionWrapper;

        public AbsSessionHandler(SessionWrapper sessionWrapper)
        {
            _sessionWrapper = sessionWrapper;
        }
        
        public HttpServerResponse HandleRequest(HttpListenerContext context, Dictionary<String, String> pathParams)
        {
            Session session = _sessionWrapper.GetSession();
            HttpServerResponse response = new HttpServerResponse();
            if (session == null)
            {
                response.StatusCode = HttpStatusCode.NoContent;
                return response;
            }

            if (session.Reconnecting())
            {
                response.StatusCode = HttpStatusCode.ServiceUnavailable;
                response.Headers.Add(HttpResponseHeader.RetryAfter, "10");
                return response;
            }

            if (!session.IsValid())
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
                return response;
            }

            return HandleRequest(response, context, session);
        }

        public string Path { get => ""; }

        public string Method { get => "GET"; }

        protected abstract HttpServerResponse HandleRequest(HttpServerResponse response, HttpListenerContext context, Session session);
    }
}