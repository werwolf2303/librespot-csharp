using System;
using lib.core;
using player;
using zeroconf;

namespace api
{
    public class SessionWrapper
    {
        protected Session _session; 
        protected ShellEvents _shellEvents;
        private Listener _listener = null;

        protected SessionWrapper(ShellEvents.Configuration shellConf)
        {
            _shellEvents = shellConf.Enabled ? new ShellEvents(shellConf) : null;
        }

        public static SessionWrapper FromZeroconf(ZeroconfServer server, ShellEvents.Configuration shellConf)
        {
            SessionWrapper wrapper = new SessionWrapper(shellConf);
            server.AddSessionListener(new SessionListenerImpl(wrapper));
            return wrapper;
        }

        public static SessionWrapper FromSession(Session session, ShellEvents.Configuration shellConf)
        {
            SessionWrapper wrapper = new SessionWrapper(shellConf);
            wrapper._session = session;
            return wrapper;
        }

        public void SetListener(Listener listener)
        {
            _listener = listener;

            Session s;
            if ((s = _session) != null) listener.OnNewSession(s);
        }

        protected void Set(Session session)
        {
            _session = session;
            session.AddCloseListener(new OnCloseListenerImpl(this)); 
            if (_shellEvents != null) session.AddReconnectionListener(_shellEvents);
            if (_listener != null) _listener.OnNewSession(_session);
        }

        protected void Clear()
        {
            Session old = _session;
            _session = null;
            if (old != null)
            { 
                old.Dispose();
            }
        }

        private class SessionListenerImpl : ZeroconfServer.SessionListener
        {
            private SessionWrapper _wrapper;

            public SessionListenerImpl(SessionWrapper wrapper)
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

        public class OnCloseListenerImpl : Session.CloseListener
        {
            private SessionWrapper _sessionWrapper;

            public OnCloseListenerImpl(SessionWrapper sessionWrapper)
            {
                _sessionWrapper = sessionWrapper;
            }

            public void OnClose()
            {
                _sessionWrapper.Clear();
            }
        }

        public Session GetSession()
        {
            Session s = _session;
            if (s != null)
            {
                if (s.IsValid()) return s;
                else Clear();
            }

            return null;
        }
        
        public interface Listener
        {
            void OnSessionCleared(Session old);
            void OnNewSession(Session session);
        }
    }
}