using System;
using System.IO;
using decoder_api;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using sink_api;

namespace lib.audio.decoders
{
  public class Mp3Decoder : Decoder
    {
        private readonly Mp3FileReader _mp3Reader;
        private readonly IWaveProvider _pcmProvider;

        public Mp3Decoder(Stream audioIn, float normalizationFactor, int duration)
            : base(audioIn, normalizationFactor, duration)
        {
            this.audioIn.Position = 0;
            _mp3Reader = new Mp3FileReader(this.audioIn);
            var volumeProvider = new VolumeSampleProvider(_mp3Reader.ToSampleProvider())
            {
                Volume = normalizationFactor
            };
            _pcmProvider = new SampleToWaveProvider16(volumeProvider);
            SetAudioFormat(new OutputAudioFormat(
                _pcmProvider.WaveFormat.SampleRate,
                _pcmProvider.WaveFormat.BitsPerSample,
                _pcmProvider.WaveFormat.Channels,
                true,
                false));
        }

        protected override int ReadInternal(Stream stream)
        {
            byte[] buffer = new byte[BUFFER_SIZE];
            int bytesRead = _pcmProvider.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                stream.Write(buffer, 0, bytesRead);
            }
            return bytesRead;
        }

        public override int Time()
        {
            return (int)_mp3Reader.CurrentTime.TotalMilliseconds;
        }

        /*public override void Seek(int positionMs)
        {
            if (positionMs < 0) positionMs = 0;
            if (positionMs > duration) positionMs = duration;

            Console.WriteLine($"Seeking to {positionMs}ms...");
            _mp3Reader.CurrentTime = TimeSpan.FromMilliseconds(positionMs);
        }*/

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mp3Reader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}