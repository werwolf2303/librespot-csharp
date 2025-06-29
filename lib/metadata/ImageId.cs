using System;
using System.Text.RegularExpressions;
using lib.common;
using spotify.metadata.proto;

namespace lib.metadata
{
    public class ImageId : SpotifyId, ISpotifyId
    {
        public static String[] ImageSizesUrlsStrings = new[]
        {
            "image_xlarge_url", "image_large_url", "image_url", "image_small_url"
        };
        internal static Regex Pattern = new Regex("spotify:image:(.{40})");
        private String _hexId;

        private ImageId(String hexId)
        {
            _spotifyId = this;
            _hexId = hexId.ToLower();
        }

        public static ImageId FromHex(String hex)
        {
            return new ImageId(hex);
        }

        public static ImageId BiggestImage(ImageGroup group)
        {
            Image biggest = null;
            foreach (Image image in group.Images)
                if (biggest == null || biggest.size.GetTypeCode() < image.size.GetTypeCode())
                    biggest = image;

            return biggest == null ? null : FromHex(Utils.bytesToHex(biggest.FileId));
        }
        
        // ToDo: public static void PutAsMetadata() needs connectstate.Player

        public string ToSpotifyUri()
        {
            return _hexId;
        }

        public string HexId()
        {
            return _hexId;
        }
    }
}