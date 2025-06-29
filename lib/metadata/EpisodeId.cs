using System;
using System.Text.RegularExpressions;
using lib.common;

namespace lib.metadata
{
    public class EpisodeId : PlayableId, IPlayableId
    {
        internal static Regex Pattern = new Regex("spotify:episode:(.{22})");
        private String _hexId;

        private EpisodeId(String hex)
        {
            _playableId = this;
            _hexId = hex.ToLower();
        }

        public static EpisodeId FromBase62(String base62)
        {
            return new EpisodeId(Utils.Base62ToHex(base62));
        }

        public static EpisodeId FromHex(String hex)
        {
            return new EpisodeId(hex);
        }
        
        public String ToMercuryUri() {
            return "hm://metadata/4/episode/" + _hexId;
        }

        public string HexId()
        {
            return _hexId;
        }

        public bool HasGid()
        {
            return true;
        }

        public byte[] GetGid()
        {
            return Utils.hexToBytes(_hexId);
        }

        public override string ToString()
        {
            return "EpisodeId{" + _playableId.ToSpotifyUri() + "}";
        }

        public override int GetHashCode()
        {
            return _hexId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            EpisodeId episodeId = (EpisodeId)obj;
            return _hexId.Equals(episodeId.HexId());
        }
    }
}