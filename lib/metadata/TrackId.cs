using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using lib.common;

namespace lib.metadata
{
    public class TrackId : PlayableId, IPlayableId
    {
        internal static Regex Pattern = new Regex("spotify:track:(.{22})");
        private String _hexId;

        private TrackId(String hex)
        {
            _playableId = this;
            _hexId = hex.ToLower();
        }

        public static TrackId FromBase62(String base62)
        {
            return new TrackId(Utils.Base62ToHex(base62, 16, Base62));
        }

        public static TrackId FromHex(String hex)
        {
            return new TrackId(hex);
        }

        public String ToMercuryUri()
        {
            return "hm://metadata/4/track/" + _hexId;
        }

        public string ToSpotifyUri()
        {
            return "spotify:track:" + Utils.HexToBase62(_hexId, 22, Base62); 
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
            return "TrackId{" + ToSpotifyUri() + "}";
        }

        public override int GetHashCode()
        {
            return _hexId.GetHashCode();
        }
        
        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            TrackId trackId = (TrackId)obj;
            return _hexId.Equals(trackId.HexId());
        }
    }
}