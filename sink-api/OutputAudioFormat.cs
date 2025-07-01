using System;
using NAudio.Wave;

namespace sink_api
{
    public class OutputAudioFormat
    {
        public static OutputAudioFormat DEFAULT_FORMAT = new OutputAudioFormat(44100, 16, 2, true, false);
        private String encoding;
        private float sampleRate;
        private int sampleSizeInBits;
        private int channels;
        private int frameSize;
        private float frameRate;
        private bool bigEndian;

        public OutputAudioFormat(float sampleRate, int sampleSizeInBits, int channels, bool signed, bool bigEndian)
        {
            encoding = signed ? "PCM_SIGNED" : "PCM_UNSIGNED";
            this.sampleRate = sampleRate;
            this.sampleSizeInBits = sampleSizeInBits;
            this.channels = channels;
            frameSize = (channels == -1 || sampleSizeInBits == -1) ? -1 : ((sampleSizeInBits + 7) / 8) * channels;
            frameRate = sampleRate;
            this.bigEndian = bigEndian;
        }
        
        public int getFrameSize() {
            return frameSize;
        }

        public float getSampleRate() {
            return sampleRate;
        }

        public bool isBigEndian() {
            return bigEndian;
        }

        public int getSampleSizeInBits() {
            return sampleSizeInBits;
        }

        public int getChannels() {
            return channels;
        }

        public String getEncoding() {
            return encoding;
        }

        public float getFrameRate() {
            return frameRate;
        }
        
        public bool matches(OutputAudioFormat format) {
            return format.getEncoding().Equals(getEncoding())
                   && (format.getChannels() == -1 || format.getChannels() == getChannels())
                   && (format.getSampleRate() == -1 || format.getSampleRate() == getSampleRate())
                   && (format.getSampleSizeInBits() == -1 || format.getSampleSizeInBits() == getSampleSizeInBits())
                   && (format.getFrameRate() == -1 || format.getFrameRate() == getFrameRate())
                   && (format.getFrameSize() == -1 || format.getFrameSize() == getFrameSize())
                   && (getSampleSizeInBits() <= 8 || format.isBigEndian() == isBigEndian());
        }
        
        public WaveFormat ToWaveFormat()
        {
            return new WaveFormat((int)getSampleRate(), getSampleSizeInBits(), getChannels());
        }
    }
}