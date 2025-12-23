using System;
using lib.core;
using player;
using zeroconf;

namespace api
{
    public class PlayerWrapper : SessionWrapper
    {
        private Player _player = null;
        private PlayerConfiguration _conf;
        private Listener _listener;

        private PlayerWrapper(PlayerConfiguration conf, ShellEvents.Configuration shellConf) : base(shellConf)
        {
            _conf = conf;
        }

        public new static PlayerWrapper FromZeroconf(ZeroconfServer server, PlayerConfiguration conf, ShellEvents.Configuration shellConf) 
        {
            PlayerWrapper wrapper = new PlayerWrapper(conf, shellConf);
            server.AddSessionListener(new SessionListenerImpl(wrapper));
            return wrapper;
        }

        public static PlayerWrapper FromSession(Session session, PlayerConfiguration conf,
            ShellEvents.Configuration shellConf)
        {
            PlayerWrapper wrapper = new PlayerWrapper(conf, shellConf);
            wrapper._session = session;
            wrapper._player = new Player(conf, session);
            return wrapper;
        }

        protected new void Set(Session session)
        {
            base.Set(session);

            Player player = new Player(_conf, session);
            _player = player;
            
            if (_shellEvents != null) _player.AddEventsListener(_shellEvents);
            if (_listener != null) _listener.OnNewPlayer(player);
        }

        protected new void Clear()
        {
            Player old = _player;
            if (old != null)
                old.Dispose();
            _player = null;
            
            if (_listener != null && old != null) _listener.OnPlayerCleared(old);
            
            base.Clear();
        }

        public Player GetPlayer()
        {
            return _player;
        }

        private class SessionListenerImpl : ZeroconfServer.SessionListener
        {
            private PlayerWrapper _wrapper;

            public SessionListenerImpl(PlayerWrapper wrapper)
            {
                _wrapper = wrapper;
            }
            
            public void SessionClosing(Session session)
            {
                if (_wrapper.GetSession() == session) 
                    _wrapper.Clear();
            }

            public void SessionChanged(Session session)
            {
                _wrapper.Set(session);
            }
        }

        private new interface Listener : SessionWrapper.Listener
        {
            void OnPlayerCleared(Player old);
            void OnNewPlayer(Player player);
        }
    }
}