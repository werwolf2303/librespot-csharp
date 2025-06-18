using System;
using csharp;
using spotify.metadata.proto;

namespace lib.audio.format
{
    public class SuperAudioFormat : Enumeration
    {
        public static readonly SuperAudioFormat MP3 = new SuperAudioFormat();
        public static readonly SuperAudioFormat VORBIS = new SuperAudioFormat();
        public static readonly SuperAudioFormat AAC = new SuperAudioFormat();
        
        private SuperAudioFormat() {}

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
                    throw new Exception("Unknown audio format: " + format);
            }
        }
    }
}