using System;
using System.Text;
using System.Web;

namespace lib.metadata
{
    public class LocalId : PlayableId, IPlayableId
    {
        private String _uri;
        private String[] _data;

        internal LocalId(String uri)
        {
            _playableId = this;
            _uri = uri;
            _data = uri.Substring("spotify:local:".Length).Split(':');
        }

        public bool HasGid()
        {
            return false;
        }

        public string ToSpotifyUri()
        {
            return _uri;
        }

        public String Artist()
        {
            return HttpUtility.UrlDecode(_data[0], Encoding.UTF8);
        }

        public String Album()
        {
            return HttpUtility.UrlDecode(_data[1], Encoding.UTF8);
        }

        public String Name()
        {
            return HttpUtility.UrlDecode(_data[2], Encoding.UTF8);
        }

        public int Duration()
        {
            return Int32.Parse(_data[3]) * 1000;
        }

        public override string ToString()
        {
            return "LocalId{" + ToSpotifyUri() + "}";
        }
    }
}