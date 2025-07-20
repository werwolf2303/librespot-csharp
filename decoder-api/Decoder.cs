using System;
using System.IO;
using log4net;
using sink_api;

namespace decoder_api
{
    public abstract class Decoder : IDisposable
    {
        public static readonly int BUFFER_SIZE = 4096;
        protected readonly Stream audioIn;
        protected readonly float normalizationFactor;
        protected readonly int duration;
        protected volatile bool closed = false;
        protected internal long seekZero = 0;
        private OutputAudioFormat format;

        protected Decoder(Stream audioIn, float normalizationFactor, int duration)
        {
            if (!audioIn.CanSeek)
            {
                throw new ArgumentException("Input stream must be seekable to support the Decoder API.", nameof(audioIn));
            }
            this.audioIn = audioIn;
            this.normalizationFactor = normalizationFactor;
            this.duration = duration;
        }

        public int WriteSomeTo(Stream stream)
        {
            if (closed) return 0;
            return ReadInternal(stream);
        }

        protected abstract int ReadInternal(Stream stream);
        public abstract int Time();

        public void Close()
        {
            if (closed) return;
            closed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            Close();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                audioIn?.Dispose();
            }
        }

        public OutputAudioFormat GetAudioFormat()
        {
            if (format == null) throw new InvalidOperationException("Audio format has not been initialized.");
            return format;
        }

        protected void SetAudioFormat(OutputAudioFormat format)
        {
            this.format = format;
        }

        protected int SampleSizeBytes()
        {
            return GetAudioFormat().getSampleSizeInBits() / 8;
        }

        public int DurationMs()
        {
            return duration;
        }
    }

    public class DecoderException : Exception
    {
        public DecoderException(string message) : base(message) { }
        public DecoderException(string message, Exception cause) : base(message, cause) { }
    }
}