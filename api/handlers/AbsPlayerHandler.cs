using System.Net;
using api.server;
using lib.core;
using player;

namespace api.handlers
{
    public abstract class AbsPlayerHandler : AbsSessionHandler
    {
        private PlayerWrapper _wrapper;
        
        public AbsPlayerHandler(PlayerWrapper wrapper) : base(wrapper)
        {
            _wrapper = wrapper;
        }

        protected override HttpServerResponse HandleRequest(HttpServerResponse response, HttpListenerContext context, Session session)
        {
            Player player = _wrapper.GetPlayer();
            if (player == null)
            {
                response.StatusCode = HttpStatusCode.NoContent;
                return response;
            }

            return HandleRequest(response, context, session, player);
        }
        
        protected abstract HttpServerResponse HandleRequest(HttpServerResponse response, HttpListenerContext context, Session session, Player player);
    }
}