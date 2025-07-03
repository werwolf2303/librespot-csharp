using System;
using System.Text.RegularExpressions;
using Connectstate;
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

        public static void PutAsMetadata(ProvidedTrack track, ImageGroup group)
        {
            foreach (Image image in group.Images)
            {
                String key;
                switch (image.size)
                {
                    case Image.Size.Default:
                        key = "image_url";
                        break;
                    case Image.Size.Small:
                        key = "image_small_url";
                        break;
                    case Image.Size.Large:
                        key = "image_large_url";
                        break;
                    case Image.Size.Xlarge:
                        key = "image_xlarge_url";
                        break;
                    default:
                        continue;
                }
                
                track.Metadatas.Add(key, FromHex(Utils.bytesToHex(image.FileId)).ToSpotifyUri());
            }
        }

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