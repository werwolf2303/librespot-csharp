using System;
using lib.core;
using log4net;
using spotify.download.proto;

namespace lib.audio.cdn
{
    public class CdnFeedHelper
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(CdnFeedHelper));

        private CdnFeedHelper()
        {
        }

        private static Uri getUrl(Session session, StorageResolveResponse resp)
        {
            return new Uri(resp.Cdnurls[new Random().Next(0, resp.Cdnurls.Count)]);
        }

        /*public static LoadedStream loadTrack(@NotNull Session session, Metadata.@NotNull Track track, Metadata.@NotNull
            AudioFile file,
        @NotNull HttpUrl url, boolean preload, @Nullable HaltListener haltListener) throws
            IOException, CdnManager.CdnException {
            long start = System.currentTimeMillis();
            byte[] key = session.audioKey().getAudioKey(track.getGid(), file.getFileId());
            int audioKeyTime = (int)(System.currentTimeMillis() - start);

            CdnManager.Streamer streamer = session.cdn().streamFile(file, key, url, haltListener);
            InputStream in = streamer.stream();
            NormalizationData normalizationData = NormalizationData.read(in);
            if (in.skip(0xa7) != 0xa7) throw new IOException("Couldn't skip 0xa7 bytes!");
            return new LoadedStream(track, streamer, normalizationData,
                new PlayableContentFeeder.Metrics(file.getFileId(), preload, preload ? -1 : audioKeyTime));
        }

        public static @NotNull LoadedStream loadTrack(@NotNull Session session, Metadata.@NotNull
            Track track, Metadata.@NotNull AudioFile file,
        @NotNull StorageResolveResponse storage, boolean preload, @Nullable HaltListener haltListener) throws
            IOException, CdnManager.CdnException {
            return loadTrack(session, track, file, getUrl(session, storage), preload, haltListener);
        }

        public static @NotNull LoadedStream loadEpisodeExternal(@NotNull Session session, Metadata.@NotNull
            Episode episode, @Nullable HaltListener haltListener) throws IOException, CdnManager.CdnException {
            try

            (Response resp = session.client().newCall(new Request.Builder().head()
                .url(episode.getExternalUrl()).build()).execute()) {

                if (resp.code() != 200)
                    LOGGER.warn("Couldn't resolve redirect!");

                HttpUrl url = resp.request().url();
                LOGGER.debug("Fetched external url for {}: {}", Utils.bytesToHex(episode.getGid()), url);

                CdnManager.Streamer streamer = session.cdn().streamExternalEpisode(episode, url, haltListener);
                return new LoadedStream(episode, streamer, null, new PlayableContentFeeder.Metrics(null, false, -1));
            }
        }

        public static @NotNull LoadedStream loadEpisode(@NotNull Session session, Metadata.@NotNull
            Episode episode, @NotNull Metadata.AudioFile file, @NotNull HttpUrl url, @Nullable
            HaltListener haltListener) throws IOException, CdnManager.CdnException {
            long start = System.currentTimeMillis();
            byte[] key = session.audioKey().getAudioKey(episode.getGid(), file.getFileId());
            int audioKeyTime = (int)(System.currentTimeMillis() - start);

            CdnManager.Streamer streamer = session.cdn().streamFile(file, key, url, haltListener);
            InputStream in = streamer.stream();
            NormalizationData normalizationData = NormalizationData.read(in);
            if (in.skip(0xa7) != 0xa7) throw new IOException("Couldn't skip 0xa7 bytes!");
            return new LoadedStream(episode, streamer, normalizationData,
                new PlayableContentFeeder.Metrics(file.getFileId(), false, audioKeyTime));
        }

        public static @NotNull LoadedStream loadEpisode(@NotNull Session session, Metadata.@NotNull
            Episode episode, @NotNull Metadata.AudioFile file, @NotNull StorageResolveResponse storage, @Nullable
            HaltListener haltListener) throws IOException, CdnManager.CdnException {
            return loadEpisode(session, episode, file, getUrl(session, storage), haltListener);
        }*/
    }
}