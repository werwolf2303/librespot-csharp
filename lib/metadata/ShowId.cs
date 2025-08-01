using System;
using System.Text.RegularExpressions;
using lib.common;

namespace lib.metadata
{
    public class ShowId : SpotifyId, ISpotifyId
    {
        internal static Regex Pattern = new Regex("spotify:show:(.{22})");
        private String _hexId;

        private ShowId(String hexId)
        {
            _spotifyId = this;
            _hexId = hexId;
        }

        public static ShowId FromBase62(String base62)
        {
            return new ShowId(Utils.Base62ToHex(base62, 16, _base62));
        }

        public static ShowId FromHex(String hex)
        {
            return new ShowId(hex);
        }

        public String ToMercuryUri()
        {
            return "hm://metadata/4/show/" + _hexId;
        }

        public string ToSpotifyUri()
        {
            return "spotify:" + PlayableIds.ToString(GetId()).ToLower() + ":" + Utils.HexToBase62(HexId(), 16, _base62);
        }

        public string HexId()
        {
            return _hexId;
        }
    }
}