using lib.common;
using lib.core;
using spotify.metadata.proto;

namespace lib.audio.storage
{
    public class StorageFeedHelper
    {
        private StorageFeedHelper()
        {
        }

        //ToDo: This class needs Session.GetAudioKey()
        
        public static PlayableContentFeeder.LoadedStream LoadTrack(Session session, Track track, AudioFile file,
            bool preload, IHaltListener haltListener)
        {
            long start = Utils.getUnixTimeStampInMilliseconds();
            byte[] key;

            return null;
        }
    }
}