using System;
using System.Text;

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
        internal static common.Base62 _base62 = common.Base62.createInstanceWithInvertedCharacterSet();

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
                    return ArtistId.FromBase62(base62);
                case PlayableIds.Playlist:
                    return new PlaylistId(base62);
                case PlayableIds.Show:
                    return ShowId.FromBase62(base62);
            }
            throw new InvalidOperationException("Unknown uri: " + uri);
        }
        
        
        public string ToSpotifyUri()
        {
            return "spotify:" + PlayableIds.ToString(_spotifyId.GetId()).ToLower() + ":" + Encoding.UTF8.GetString(_base62.encode(Encoding.UTF8.GetBytes(_spotifyId.HexId())));
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