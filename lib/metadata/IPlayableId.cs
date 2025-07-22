using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Connectstate;
using spotify.player.proto;
using lib.common;
using spotify.metadata.proto;

namespace lib.metadata
{
    public interface IPlayableId
    {
        String ToSpotifyUri();
        bool HasGid();
        byte[] GetGid();
        String HexId();
        int GetId();
        bool Matches(ContextTrack current);
    }

    public class PlayableIds
    {
        public const int Unsupported = -1;
        
        public const int Album = 0;
        public const int Artist = 1;
        public const int Episode = 2;
        public const int Image = 3;
        public const int Local = 4;
        public const int Playlist = 5;
        public const int Show = 6;
        public const int Track = 7;
        
        public static int FromSpotifyUri(String uri)
        {
            String id = uri.Split(':')[1].ToLower();
            foreach (FieldInfo field in typeof(PlayableIds).GetFields())
                if (id.Equals(field.Name.ToLower()))
                    return (int)field.GetValue(null);

            return -1;
        }

        public static String ToString(int id)
        {
            foreach (FieldInfo field in typeof(PlayableIds).GetFields())
                if (id == (int)field.GetValue(null))
                    return field.Name;
            return "";
        }
    }

    public class PlayableId : IPlayableId
    {
        internal static IPlayableId _playableId;
        public static common.Base62 Base62 = common.Base62.CreateInstanceWithInvertedCharacterSet();
        
        protected PlayableId()
        {
        }
        
        public static IPlayableId FromUri(String uri)
        {
            if (IsDelimiter(uri)) return new UnsupportedId(uri);
            if (IsLocal(uri)) return new LocalId(uri);
            if (TrackId.Pattern.IsMatch(uri)) return TrackId.FromBase62(uri.Split(':')[2]);
            if (EpisodeId.Pattern.IsMatch(uri)) return EpisodeId.FromBase62(uri.Split(':')[2]);
            throw new InvalidOperationException("Unknown uri: " + uri);
        }
        
        public static int IndexOfTrack(List<ContextTrack> tracks, IPlayableId id)
        {
            byte[] gid = null;
            if (id.HasGid()) gid = id.GetGid();
            
            String uri = id.ToSpotifyUri();
            for (int i = 0; i < tracks.Count; i++)
            {
                ContextTrack track = tracks[i];
                if ((track.Uri != null && uri.Equals(track.Uri)) || (track.Gid != null && track.Gid.Equals(gid)))
                    return i;
            }
            
            return -1;
        }

        public static IPlayableId From(ProvidedTrack track)
        {
            return track.Uri.Equals("") ? null : FromUri(track.Uri);
        }

        public static bool IsDelimiter(String uri)
        {
            return uri.Equals("spotify:delimiter") || uri.Equals("spotify:meta:delimiter");
        }

        public static bool IsLocal(String uri)
        {
            return uri.StartsWith("spotify:local:");
        }

        public static IPlayableId From(ContextTrack track)
        {
            return FromUri(track.Uri);
        }

        public static PlayableId From(Track track)
        {
            return TrackId.FromHex(Utils.bytesToHex(track.Gid));
        }

        public static PlayableId From(Episode episode)
        {
            return EpisodeId.FromHex(Utils.bytesToHex(episode.Gid));
        }
        
        public static String InferUriPrefix(String contextUri)
        {
            if (contextUri.StartsWith("spotify:episode:") || contextUri.StartsWith("spotify:show:"))
                return "spotify:episode:";
            return "spotify:track:";
        }

        public string ToSpotifyUri()
        {
            return "spotify:" + PlayableIds.ToString(_playableId.GetId()).ToLower() + ":" + Utils.HexToBase62(_playableId.HexId(), 16, Base62);
        }

        public int GetId()
        {
            return _playableId.GetId();
        }
        
        public bool HasGid()
        {
            throw new InvalidOperationException();
        }

        public byte[] GetGid()
        {
            throw new InvalidOperationException();
        }

        public string HexId()
        {
            throw new InvalidOperationException();
        }

        public bool Matches(ContextTrack current)
        {
            if (current.Uri != null)
                return ToSpotifyUri().Equals(current.Uri);
            if (current.Gid != null)
                return current.Gid.Equals(GetGid());
            return false;
        }
    }
}