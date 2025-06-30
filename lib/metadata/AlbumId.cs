using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Base62;
using lib.common;
using Org.BouncyCastle.Utilities.Encoders;
using spotify.player.proto;

namespace lib.metadata
{
    public class AlbumId : SpotifyId, ISpotifyId
    {
        internal static readonly Regex Pattern = new Regex("spotify:album:(.{22})");
        private String _hexId;

        private AlbumId(String hex)
        {
            _spotifyId = this;
            _hexId = hex.ToLower();
            Console.WriteLine(_hexId);
        }

        public static AlbumId FromBase62(String base62)
        {
            return new AlbumId(Utils.Base62ToHex(base62, 16, _base62));
        }

        public static AlbumId FromHex(String hex)
        {
            return new AlbumId(hex);
        }

        public String ToMercuryUri()
        {
            return "hm://metadata/4/album/" + _hexId;
        }

        public int GetId()
        {
            return PlayableIds.Album;
        }

        public string HexId()
        {
            return _hexId;
        }
    }
}