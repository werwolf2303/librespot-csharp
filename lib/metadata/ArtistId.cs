using System;
using System.Text.RegularExpressions;
using lib.common;

namespace lib.metadata
{
    public class ArtistId : SpotifyId, ISpotifyId
    {
        internal static Regex Pattern = new Regex("spotify:artist:(.{22})");
        private String _hexId;

        private ArtistId(String hex)
        {
            _spotifyId = this;
            _hexId = hex.ToLower();
        }

        public static ArtistId FromBase62(String base62)
        {
            return new ArtistId(Utils.Base62ToHex(base62, 16, _base62));
        }

        public static ArtistId FromHex(String hex)
        {
            return new ArtistId(hex);
        }

        public String ToMercuryUri()
        {
            return "hm://metadata/4/artist/" + _hexId;
        }

        public int GetId()
        {
            return PlayableIds.Artist;
        }

        public string HexId()
        {
            return _hexId;
        }
    }
}