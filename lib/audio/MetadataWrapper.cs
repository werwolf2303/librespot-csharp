using System;
using lib.common;
using lib.metadata;
using spotify.metadata.proto;

namespace lib.audio
{
    public class MetadataWrapper
    {
        public PlayableId _id;
        public Track _track;
        public Episode _episode;
        public LocalId _localTrack;

        public MetadataWrapper(Track track, Episode episode, LocalId localTrack)
        {
            if (track == null && episode == null && localTrack == null) throw new ArgumentException("Illegal argument");
            
            _track = track;
            _episode = episode;
            _localTrack = localTrack;
            
            if (_track != null) _id = PlayableId.From(_track);
            else if (_episode != null) _id = PlayableId.From(_episode);
            else _id = localTrack;
        }

        public bool IsTrack()
        {
            return _track != null;
        }

        public bool IsEpisode()
        {
            return _episode != null;
        }

        public bool IsLocalTrack()
        {
            return _localTrack != null;
        }

        public int Duration()
        {
            if (_track != null) return _track.Duration;
            if (_episode != null) return _episode.Duration;
            return _localTrack.Duration();
        }

        public ImageGroup GetCoverImage()
        {
            if (_track != null)
            {
                if (_track.Album != null && _track.Album.CoverGroup != null)
                    return _track.Album.CoverGroup;
            } else if (_episode != null)
            {
                if (_episode.CoverImage != null)
                    return _episode.CoverImage;
            }
            else
            {
                // ToDo: Fetch album image from track file
            }

            return null;
        }

        public String GetName()
        {
            if (_track != null) return _track.Name;
            if (_episode != null) return _episode.Name;
            return _localTrack.Name();
        }

        public String GetAlbumName()
        {
            if (_track != null) return _track.Album.Name;
            if (_episode != null) return _episode.Show.Name;
            return _localTrack.Album();
        }
        
        public String GetArtist() {
            if (_track != null) return Utils.ArtistsToString(_track.Artists);
            if (_episode != null) return _episode.Show.Publisher;
            return _localTrack.Artist();
        }
    }
}