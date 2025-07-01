using System;
using System.IO;
using decoder_api;
using deps.NVorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using sink_api;

namespace lib.audio.decoders
{
        public class VorbisDecoder : Decoder
    {
        private readonly VorbisReader _vorbisReader;
        private readonly float[] _readBuffer;
        private readonly byte[] _byteBuffer;

        public VorbisDecoder(Stream audioIn, float normalizationFactor)
            // Initialize base with a temporary VorbisReader to get duration, then dispose it.
            : base(audioIn, normalizationFactor, GetDuration(audioIn))
        {
            // Reset stream for the main reader instance.
            this.audioIn.Position = 0;

            // _vorbisReader is the core NVorbis component for decoding.
            _vorbisReader = new VorbisReader(this.audioIn, false);

            // Create buffers for reading. NVorbis reads into a float[], which we convert to byte[].
            _readBuffer = new float[BUFFER_SIZE];
            _byteBuffer = new byte[BUFFER_SIZE * 2]; // 1 float = 2 bytes (16-bit)

            // Set the audio format. We are converting to 16-bit signed little-endian PCM.
            SetAudioFormat(new OutputAudioFormat(
                _vorbisReader.SampleRate,
                16, // We will output 16-bit PCM
                _vorbisReader.Channels,
                true,  // Signed PCM
                false)); // Little-endian
        }

        // Helper to get duration without leaving a reader open.
        private static int GetDuration(Stream audioIn)
        {
            using (var reader = new VorbisReader(audioIn, false))
            {
                return (int)reader.TotalTime.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Reads float samples from NVorbis, converts them to 16-bit PCM bytes, and writes to the output stream.
        /// </summary>
        protected override int ReadInternal(Stream stream)
        {
            // Read decoded samples from NVorbis into the float buffer.
            int samplesRead = _vorbisReader.ReadSamples(_readBuffer, 0, _readBuffer.Length);
            if (samplesRead == 0)
            {
                return 0; // End of stream
            }

            // Convert float samples to 16-bit PCM bytes.
            int bytesToWrite = 0;
            for (int i = 0; i < samplesRead; i++)
            {
                // Apply normalization factor (volume).
                float sample = _readBuffer[i] * normalizationFactor;

                // Clamp the value to the [-1.0, 1.0] range.
                if (sample > 1.0f) sample = 1.0f;
                if (sample < -1.0f) sample = -1.0f;

                // Convert to 16-bit integer.
                short pcmSample = (short)(sample * 32767);

                // Convert to bytes (little-endian).
                _byteBuffer[bytesToWrite++] = (byte)(pcmSample & 0xFF);
                _byteBuffer[bytesToWrite++] = (byte)(pcmSample >> 8);
            }

            // Write the converted byte buffer to the output stream.
            stream.Write(_byteBuffer, 0, bytesToWrite);
            
            return bytesToWrite;
        }

        /// <summary>
        /// Returns the current time from the NVorbis reader.
        /// </summary>
        public override int Time()
        {
            return (int)_vorbisReader.DecodedTime.TotalMilliseconds;
        }

        /// <summary>
        /// Seeks using the NVorbis reader's time-based seeking.
        /// </summary>
        public override void Seek(int positionMs)
        {
            if (positionMs < 0) positionMs = 0;
            if (positionMs > duration) positionMs = duration;

            Console.WriteLine($"Seeking to {positionMs}ms...");
            _vorbisReader.DecodedTime = TimeSpan.FromMilliseconds(positionMs);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _vorbisReader?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}