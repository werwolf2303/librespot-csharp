using System;
using System.Collections.Generic;
using csharp;
using spotify.metadata.proto;

namespace lib.audio.decoders
{
    public class AudioQuality : Enumeration
    {
        public static readonly AudioQuality NORMAL = new AudioQuality();
        public static readonly AudioQuality HIGH = new AudioQuality();
        public static readonly AudioQuality VERY_HIGH = new AudioQuality();
        
        private AudioQuality() {}

        private static AudioQuality getQuality(AudioFile.Format format)
        {
            switch (format)
            {
                case AudioFile.Format.Mp396:
                case AudioFile.Format.OggVorbis96:
                case AudioFile.Format.Aac24Norm: 
                    return NORMAL;
                case AudioFile.Format.Mp3160:
                case AudioFile.Format.Mp3160Enc:
                case AudioFile.Format.OggVorbis160:
                case AudioFile.Format.Aac24:
                    return HIGH;
                case AudioFile.Format.Mp3320:
                case AudioFile.Format.Mp3256:
                case AudioFile.Format.OggVorbis320:
                case AudioFile.Format.Aac48:
                    return VERY_HIGH;
                default:
                    throw new Exception("Unknown format: " + format);
            }
        }

        public List<AudioFile> getMatches(List<AudioFile> files)
        {
            List<AudioFile> list = new List<AudioFile>(files.Count);
            foreach (AudioFile file in Enum.GetValues(typeof(AudioFile)))
            {
                if (getQuality(file.format) != null)
                {
                    list.Add(file);
                }
            }

            return list;
        }
    }
}