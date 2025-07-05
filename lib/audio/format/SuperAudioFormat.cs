using System;
using csharp;
using log4net;
using spotify.metadata.proto;

namespace lib.audio.format
{
    public class SuperAudioFormat : Enumeration
    {
        public static readonly SuperAudioFormat MP3 = new SuperAudioFormat(0);
        public static readonly SuperAudioFormat VORBIS = new SuperAudioFormat(1);
        public static readonly SuperAudioFormat AAC = new SuperAudioFormat(2);
        
        private static ILog LOGGER = log4net.LogManager.GetLogger(typeof(SuperAudioFormat));
        
        private SuperAudioFormat() {}
        public int Id;
        
        private SuperAudioFormat(int id)
        {
            Id = id;
        }

        public static SuperAudioFormat FromId(int id)
        {
            switch (id)
            {
                case 0:
                    return MP3;
                case 1:
                    return VORBIS;
                default:
                    return AAC;
            }
        }

        public static SuperAudioFormat get(AudioFile.Format format)
        {
            switch (format) {
                case AudioFile.Format.OggVorbis96:
                case AudioFile.Format.OggVorbis160:
                case AudioFile.Format.OggVorbis320:
                    return VORBIS;
                case AudioFile.Format.Mp3256:
                case AudioFile.Format.Mp3320:
                case AudioFile.Format.Mp3160:
                case AudioFile.Format.Mp396:
                case AudioFile.Format.Mp3160Enc:
                    return MP3;
                case AudioFile.Format.Aac24:
                case AudioFile.Format.Aac48:
                case AudioFile.Format.Aac24Norm:
                    return AAC;
                default:
                    LOGGER.Warn("Unsupported audio format '" + format + "'");
                    return null;
            }
        }
    }
}