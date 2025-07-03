using System;
using System.IO;
using System.Net;
using EasyHttp.Http;
using lib.common;
using lib.core;
using log4net;
using spotify.download.proto;
using spotify.metadata.proto;

namespace lib.audio.cdn
{
    public class CdnFeedHelper
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(CdnFeedHelper));

        private CdnFeedHelper()
        {
        }

        private static Uri GetUrl(Session session, StorageResolveResponse resp)
        {
            return new Uri(resp.Cdnurls[new Random().Next(0, resp.Cdnurls.Count)]);
        }

        public static PlayableContentFeeder.LoadedStream LoadTrack(Session session, Track track, AudioFile file,
            Uri url, bool preload, IHaltListener haltListener)
        {
            long start = Utils.getUnixTimeStampInMilliseconds();
            byte[] key = session.GetAudioKey().GetAudioKey(track.Gid, file.FileId);
            int audioKeyTime = (int)(Utils.getUnixTimeStampInMilliseconds() - start);
            
            CdnManager.Streamer streamer = session.GetCdn().StreamFile(file, key, url, haltListener);
            AbsChunkedInputStream stream = streamer.Stream();
            NormalizationData normalizationData = NormalizationData.Read(stream);
            if (stream.Skip(0xa7) != 0xa7) throw new IOException("Couldn't skip 0xa7 bytes!");
            return new PlayableContentFeeder.LoadedStream(track, streamer, normalizationData,
                new PlayableContentFeeder.Metrics(file.FileId, preload, preload ? -1 : audioKeyTime));
        }

        public static PlayableContentFeeder.LoadedStream LoadTrack(Session session, Track track, AudioFile file,
            StorageResolveResponse storage, bool preload, IHaltListener haltListener)
        {
            return LoadTrack(session, track, file, GetUrl(session, storage), preload, haltListener);
        }

        public static PlayableContentFeeder.LoadedStream LoadEpisodeExternal(Session session, Episode episode,
            IHaltListener haltListener)
        {
            HttpResponse resp = session.GetClient().Head(episode.ExternalUrl);
            
            if(resp.StatusCode != HttpStatusCode.OK) 
                LOGGER.Warn("Couldn't resolve redirect!");

            Uri url = new Uri(resp.Location);
            LOGGER.DebugFormat("Fetched external url for {0}: {1}", Utils.bytesToHex(episode.Gid), url);

            CdnManager.Streamer streamer = session.GetCdn().StreamExternalEpisode(episode, url, haltListener);
            return new PlayableContentFeeder.LoadedStream(episode, streamer, null,
                new PlayableContentFeeder.Metrics(null, false, -1));
        }

        public static PlayableContentFeeder.LoadedStream LoadEpisode(Session session, Episode episode, AudioFile file,
            Uri url, IHaltListener haltListener)
        {
            long start = Utils.getUnixTimeStampInMilliseconds();
            byte[] key = session.GetAudioKey().GetAudioKey(episode.Gid, file.FileId);
            int audioKeyTime = (int)(Utils.getUnixTimeStampInMilliseconds() - start);
            
            CdnManager.Streamer streamer = session.GetCdn().StreamFile(file, key, url, haltListener);
            AbsChunkedInputStream stream = streamer.Stream();
            NormalizationData normalizationData = NormalizationData.Read(stream);
            if (stream.Skip(0xa7) != 0xa7) throw new IOException("Couldn't skip 0xa7 bytes!");
            return new PlayableContentFeeder.LoadedStream(episode, streamer, normalizationData,
                new PlayableContentFeeder.Metrics(file.FileId, false, audioKeyTime));
        }

        public static PlayableContentFeeder.LoadedStream LoadEpisode(Session session, Episode episode, AudioFile file,
            StorageResolveResponse storage, IHaltListener haltListener)
        {
            return LoadEpisode(session, episode, file, GetUrl(session, storage), haltListener);
        }
    }
}