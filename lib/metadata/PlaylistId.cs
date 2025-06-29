using System;
using System.Text.RegularExpressions;

namespace lib.metadata
{
    public class PlaylistId : SpotifyId, ISpotifyId
    {
        internal static Regex Pattern = new Regex("spotify:track:(.{22})");
        public String _id;

        internal PlaylistId(String id)
        {
            _spotifyId = this;
            _id = id;
        }

        public String Id()
        {
            return _id;
        }

        public string ToSpotifyUri()
        {
            return "spotify:playlist:" + _id;
        }
    }
}