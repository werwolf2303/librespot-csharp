using System;
using System.Collections.Generic;
using lib.audio.format;
using lib.common;
using log4net;
using spotify.metadata.proto;

namespace lib.audio.decoders
{
    public class VorbisOnlyAudioQuality
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(VorbisOnlyAudioQuality));
        private AudioQuality preferred;

        public VorbisOnlyAudioQuality(AudioQuality preferred)
        {
            this.preferred = preferred;
        }

        public static AudioFile getVorbisFile(List<AudioFile> files)
        {
            foreach(AudioFile file in Enum.GetValues(typeof(AudioFile)))
            {
                if (SuperAudioFormat.get(file.format).Equals(SuperAudioFormat.VORBIS))
                {
                    return file;
                }
            }

            return null;
        }

        public AudioFile getFile(List<AudioFile> files)
        {
            List<AudioFile> matches = preferred.getMatches(files);
            AudioFile vorbis = getVorbisFile(matches);
            if (vorbis == null) {
                vorbis = getVorbisFile(files);
                if (vorbis != null)
                    LOGGER.Warn("Using " + vorbis.format + " because preferred " + preferred + " couldn't be found.");
                else
                    LOGGER.Error("Couldn't find any Vorbis file, available: " + Utils.formatsToString(files));
            }

            return vorbis;
        }
    }
}