using System;
using System.Text;
using lib.common;

namespace lib.metadata
{
    public interface ISpotifyId
    {
        String ToSpotifyUri();
        int GetId();
        String HexId();
    }

    public class SpotifyId : ISpotifyId
    {
        internal static ISpotifyId _spotifyId;
        internal static common.Base62 _base62 = common.Base62.CreateInstanceWithInvertedCharacterSet();

        public static ISpotifyId FromUri(String uri)
        {
            String base62 = uri.Split(':')[2];
            switch (PlayableIds.FromSpotifyUri(uri))
            {
                case PlayableIds.Album:
                    return AlbumId.FromBase62(base62);
                case PlayableIds.Artist:
                    return ArtistId.FromBase62(base62);
                case PlayableIds.Image:
                    return ImageId.FromHex(base62);
                case PlayableIds.Playlist:
                    return new PlaylistId(base62);
                case PlayableIds.Show:
                    return ShowId.FromBase62(base62);
            }
            throw new InvalidOperationException("Unknown uri: " + uri);
        }
        
        
        public string ToSpotifyUri()
        {
            return _spotifyId.ToSpotifyUri();
        }

        public int GetId()
        {
            throw new InvalidOperationException();
        }

        public string HexId()
        {
            throw new InvalidOperationException();
        }
    }
}