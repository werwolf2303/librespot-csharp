using System;

namespace player.contexts
{
    public class GeneralInfiniteContext : AbsSpotifyContext
    {
        internal GeneralInfiniteContext(String context) : base(context)
        {
        }

        public override bool IsFinite()
        {
            return false;
        }
    }
}