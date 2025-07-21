using System;

namespace player.contexts
{
    public class GeneralFiniteContext : AbsSpotifyContext
    {
        internal GeneralFiniteContext(String context) : base(context)
        {
        }

        public override bool IsFinite()
        {
            return true;
        }
    }
}