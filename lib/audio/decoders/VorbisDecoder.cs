using System;
using System.IO;
using decoder_api;
using deps.NAudio.Vorbis;
using deps.NVorbis;
using lib.audio.format;
using sink_api;

namespace lib.audio.decoders
{
    public class VorbisDecoder : Decoder
    {
        private readonly VorbisWaveReader _waveReader;
        private readonly float[] _readBuffer;
        private readonly byte[] _byteBuffer;
        private readonly int _channels;
        private const int CHUNK_FRAMES = 1024; // frames = samples per channel
        private OutputAudioFormat _format = OutputAudioFormat.DEFAULT_FORMAT;

        public VorbisDecoder(Stream audioIn, float normalizationFactor, int duration)
            : base(audioIn, normalizationFactor, duration)
        {
            audioIn.Position = 0;
            _waveReader = new VorbisWaveReader(audioIn);

            _channels = _format.getChannels();

            _readBuffer = new float[CHUNK_FRAMES * _channels]; // interleaved float samples
            _byteBuffer = new byte[_readBuffer.Length * 2];     // 16-bit PCM

            SetAudioFormat(new OutputAudioFormat(
                _format.getSampleRate(),
                16,
                _channels,
                signed: true,
                bigEndian: false));
        }

        protected override int ReadInternal(Stream stream)
        {
            try
            {
                // Read a chunk of samples (interleaved)
                int samplesRead = _waveReader.Read(_readBuffer, 0, _readBuffer.Length);
                if (samplesRead == 0)
                    return 0; // EOF

                int bytesToWrite = 0;

                for (int i = 0; i < samplesRead; i++)
                {
                    float sample = _readBuffer[i] * normalizationFactor;

                    // Clamp to [-1.0, 1.0]
                    sample = Math.Max(-1.0f, Math.Min(1.0f, sample));
                    short pcmSample = (short)(sample * 32767f);

                    _byteBuffer[bytesToWrite++] = (byte)(pcmSample & 0xFF);
                    _byteBuffer[bytesToWrite++] = (byte)(pcmSample >> 8);
                }

                stream.Write(_byteBuffer, 0, bytesToWrite);
                return bytesToWrite;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VorbisDecoder] Decode error: {ex.Message}");

                // Write silence to maintain timing
                int silentBytes = CHUNK_FRAMES * _channels * 2;
                Array.Clear(_byteBuffer, 0, silentBytes);
                stream.Write(_byteBuffer, 0, silentBytes);
                return silentBytes;
            }
        }

        public override int Time()
        {
            return (int)_waveReader.TotalTime.TotalMilliseconds;
        }

        public override void Seek(int positionMs)
        {
            if (positionMs < 0) positionMs = 0;
            if (positionMs > duration) positionMs = duration;

            Console.WriteLine($"Seeking to {positionMs}ms...");
            _waveReader.CurrentTime = TimeSpan.FromMilliseconds(positionMs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waveReader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
