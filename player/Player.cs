using System;
using lib.audio;
using lib.metadata;

namespace player
{
    public class Player
    {
        public static int VOLUME_MAX = 65536;

        public interface IEventsListener
        {
            void OnContextChanged(Player player, String newUri);

            void OnTrackChanged(Player player, IPlayableId id, MetadataWrapper metadata);

            void OnPlaybackEnded(Player player);

            void OnPlaybackPaused(Player player, long trackTime);
            
            void OnPlaybackResumed(Player player, long trackTime);
            
            void OnPlaybackFailed(Player player, Exception ex);
            
            void OnTrackSeeked(Player player, long trackTime);
            
            void OnMetadataAvailable(Player player, MetadataWrapper metadata);
        }
    }
}