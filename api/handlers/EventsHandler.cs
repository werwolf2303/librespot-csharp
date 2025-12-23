using System;
using System.Collections.Generic;
using System.Net;
using api.server;
using deps.WebSocketSharp;
using deps.WebSocketSharp.Net.WebSockets;
using lib.audio;
using lib.core;
using lib.metadata;
using log4net;
using player;

namespace api.handlers
{
    public class EventsHandler : Player.IEventsListener, PlayerWrapper.Listener, Session.ReconnectionListener, IHttpServerEndpointHandler
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(EventsHandler));
        private static List<HttpListenerWebSocketContext> _webSocketContexts = new List<HttpListenerWebSocketContext>();

        public EventsHandler()
        {
            
        }
        
        public void OnContextChanged(Player player, string newUri)
        {
            throw new NotImplementedException();
        }

        public void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata, bool userInitiated)
        {
            throw new NotImplementedException();
        }

        public void OnPlaybackEnded(Player player)
        {
            throw new NotImplementedException();
        }

        public void OnPlaybackPaused(Player player, long trackTime)
        {
            throw new NotImplementedException();
        }

        public void OnPlaybackResumed(Player player, long trackTime)
        {
            throw new NotImplementedException();
        }

        public void OnPlaybackFailed(Player player, Exception ex)
        {
            throw new NotImplementedException();
        }

        public void OnTrackSeeked(Player player, long trackTime)
        {
            throw new NotImplementedException();
        }

        public void OnMetadataAvailable(Player player, MetadataWrapper metadata)
        {
            throw new NotImplementedException();
        }

        public void OnPlaybackHaltStateChanged(Player player, bool halted, long trackTime)
        {
            throw new NotImplementedException();
        }

        public void OnInactiveSession(Player player, bool timeout)
        {
            throw new NotImplementedException();
        }

        public void OnVolumeChanged(Player player, float volume)
        {
            throw new NotImplementedException();
        }

        public void OnPanicState(Player player)
        {
            throw new NotImplementedException();
        }

        public void OnStartedLoading(Player player)
        {
            throw new NotImplementedException();
        }

        public void OnFinishedLoading(Player player)
        {
            throw new NotImplementedException();
        }

        public void OnSessionCleared(Session old)
        {
            throw new NotImplementedException();
        }

        public void OnNewSession(Session session)
        {
            throw new NotImplementedException();
        }

        public void OnConnectionDropped()
        {
            throw new NotImplementedException();
        }

        public void OnConnectionEstablished()
        {
            throw new NotImplementedException();
        }

        public HttpServerResponse HandleRequest(HttpListenerContext context, Dictionary<string, string> pathParameters)
        {
            HttpListenerRequest request = context.Request;
            HttpServerResponse response = new HttpServerResponse();
            if (request.Headers.Get("Upgrade") == null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Content = "Missing upgrade header";
                return response;
            }
            if (request.Headers.Get("Upgrade").ToLower() != "websocket")
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                response.Content = "Invalid upgrade header";
                return response;
            }

            return null;
        }

        public string Path { get; }
        public string Method { get; }
    }
}