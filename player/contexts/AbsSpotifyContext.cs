using System;
using lib.core;
using player.state;

namespace player.contexts
{
    public abstract class AbsSpotifyContext
    {
        public RestrictionsManager Restrictions;
        protected String Context;

        internal AbsSpotifyContext(String context)
        {
            Context = context;
            Restrictions = new RestrictionsManager(this);
        }

        public static bool IsCollection(Session session, String uri)
        {
            return Object.Equals(uri, "spotify:user:" + session.Username() + ":collection");
        }

        public static AbsSpotifyContext From(String context)
        {
            if (context.StartsWith("spotify:dailymix:") || context.StartsWith("spotify:station:"))
                return new GeneralInfiniteContext(context);
            else if (context.StartsWith("spotify:search:"))
                return new SearchContext(context, context.Substring(15));
            else
                return new GeneralFiniteContext(context);
        }

        public override string ToString()
        {
            return "AbsSpotifyContext(context='" + Context + "')";
        }

        public abstract bool IsFinite();
        
        public String Uri() { return Context; }

        public class UnsupportedContextException : Exception
        {
            internal UnsupportedContextException(String message) : base(message) { }

            public static UnsupportedContextException CannotPlayAnything()
            {
                return new UnsupportedContextException("Nothing from this context can or should be played!");
            }
        }
    }
}