using System.IO;
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
        
        public static PlayableContentFeeder.LoadedStream LoadTrack(Session session, Track track, spotify.metadata.proto.AudioFile file,
            bool preload, IHaltListener haltListener)
        {
            long start = Utils.getUnixTimeStampInMilliseconds();
            byte[] key = session.GetAudioKey().GetAudioKey(track.Gid, file.FileId);
            int audioKeyTime = (int) (Utils.getUnixTimeStampInMilliseconds() - start);

            AudioFileStreaming stream = new AudioFileStreaming(session, file, key, haltListener);
            stream.Open();

            AbsChunkedInputStream inputStream = stream.Stream();
            inputStream.Initialize();
            NormalizationData normalizationData = NormalizationData.Read(inputStream);
            if (inputStream.Seek(0xa7, SeekOrigin.Begin) != 0xa7)
            {
                throw new IOException("Couldn't skip 0xa7 bytes!");
            }
            
            return new PlayableContentFeeder.LoadedStream(track, stream, normalizationData, new PlayableContentFeeder.Metrics(file.FileId, preload, preload ? -1 : audioKeyTime));
        }

        public static PlayableContentFeeder.LoadedStream LoadEpisode(Session session, Episode episode,
            spotify.metadata.proto.AudioFile file, bool preload, IHaltListener haltListener)
        {
            long start = Utils.getUnixTimeStampInMilliseconds();
            byte[] key = session.GetAudioKey().GetAudioKey(episode.Gid, file.FileId);
            int audioKeyTime = (int) (Utils.getUnixTimeStampInMilliseconds() - start);
            
            AudioFileStreaming stream = new AudioFileStreaming(session, file, key, haltListener);
            stream.Open();
            
            AbsChunkedInputStream inputStream = stream.Stream();
            inputStream.Initialize();
            NormalizationData normalizationData = NormalizationData.Read(inputStream);
            if (inputStream.Seek(0xa7, SeekOrigin.Begin) != 0xa7)
            {
                throw new IOException("Couldn't skip 0xa7 bytes!");
            }
            
            return new PlayableContentFeeder.LoadedStream(episode, stream, normalizationData, new PlayableContentFeeder.Metrics(file.FileId, preload, preload ? -1 : audioKeyTime));
        }
    }
}