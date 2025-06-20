using System;
using System.IO;
using log4net;
using sink_api;

namespace decoder_api
{
    public abstract class Decoder : IDisposable
    {
        public static int BUFFER_SIZE = 2048;
        private static ILog LOGGER = LogManager.GetLogger(typeof(Decoder));
        protected SeekableInputStream audioIn;
        protected float normalizationFactor;
        protected int duration;
        protected volatile bool closed = false;
        protected int seekZero = 0;
        private OutputAudioFormat format;

        public Decoder(SeekableInputStream audioIn, float normalizationFactor, int duration)
        {
            this.audioIn = audioIn;
            this.normalizationFactor = normalizationFactor;
            this.duration = duration;
        }

        public int writeSomeTo(Stream stream)
        {
            return readInternal(stream);
        }
        
        protected abstract int readInternal(Stream stream);

        public abstract int time();

        public void close()
        {
            closed = true;
            audioIn.Close();
            Close();
        }

        public abstract void Close();

        public void seek(int positionMs)
        {
            if (positionMs < 0) positionMs = 0;

            audioIn.seek(seekZero);
            if (positionMs > 0)
            { 
                int skip = (int)Math.Round(audioIn.Length / (float) duration * positionMs);
                if (skip > audioIn.Length) skip = (int)audioIn.Length;

                long skipped = audioIn.skip(skip);
                if (skip != skipped)
                {
                    throw new IOException(String.Format("Failed setting, skip: {0}, skipped: {1}", skip, skipped));
                }
            }
        }

        public OutputAudioFormat getAudioFormat()
        {
            if (format == null) throw new Exception("Illegal state");
            return format;
        }

        protected void setAudioFormat(OutputAudioFormat format)
        {
            this.format = format;
        }

        protected int sampleSizeBytes()
        {
            return getAudioFormat().getSampleSizeInBits() / 8;
        }

        public int Duration()
        {
            return duration;
        }

        public int size()
        {
            return audioIn.size();
        }

        public void Dispose()
        {
            audioIn?.Dispose();
        }
    }

    public class CannotGetTimeException : Exception
    {
        public CannotGetTimeException(String message) : base(message)
        {
        }

        public CannotGetTimeException(String message, Exception cause) : base(message, cause)
        {
        }
    }
    
    public class DecoderException : Exception
    {
        public DecoderException(String message) : base(message)
        {
        }

        public DecoderException(String message, Exception cause) : base(message, cause)
        {
        }
    }
}