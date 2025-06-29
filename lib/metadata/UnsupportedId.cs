using System;

namespace lib.metadata
{
    public class UnsupportedId : PlayableId, IPlayableId
    {
        private String _uri;

        internal UnsupportedId(String uri)
        {
            _playableId = this;
            _uri = uri;
        }

        public bool HasGid()
        {
            return false;
        }

        public string ToSpotifyUri()
        {
            return _uri;
        }

        public override string ToString()
        {
            return "UnsupportedId{" + ToSpotifyUri() + "}";
        }
    }
}