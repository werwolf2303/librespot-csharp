using System;
using System.IO;
using decoder_api;
using deps.NVorbis;
using sink_api;

namespace lib.audio.decoders
{
        public class VorbisDecoder : Decoder
    {
        private readonly VorbisReader _vorbisReader;
        private readonly float[] _readBuffer;
        private readonly byte[] _byteBuffer;

        public VorbisDecoder(Stream audioIn, float normalizationFactor)
            : base(audioIn, normalizationFactor, GetDuration(audioIn))
        {
            this.audioIn.Position = 0;

            _vorbisReader = new VorbisReader(this.audioIn, false);

            _readBuffer = new float[BUFFER_SIZE];
            _byteBuffer = new byte[BUFFER_SIZE * 2]; 

            SetAudioFormat(new OutputAudioFormat(
                _vorbisReader.SampleRate,
                16, 
                _vorbisReader.Channels,
                true, 
                false));
        }

        private static int GetDuration(Stream audioIn)
        {
            using (var reader = new VorbisReader(audioIn, false))
            {
                return (int)reader.TotalTime.TotalMilliseconds;
            }
        }
        
        protected override int ReadInternal(Stream stream)
        {
            Console.WriteLine("Decoding a piece");
            
            int samplesRead = _vorbisReader.ReadSamples(_readBuffer, 0, _readBuffer.Length);
            if (samplesRead == 0)
            {
                return 0; 
            }
            
            int bytesToWrite = 0;
            for (int i = 0; i < samplesRead; i++)
            {
                float sample = _readBuffer[i] * normalizationFactor;

                if (sample > 1.0f) sample = 1.0f;
                if (sample < -1.0f) sample = -1.0f;
                short pcmSample = (short)(sample * 32767);

                _byteBuffer[bytesToWrite++] = (byte)(pcmSample & 0xFF);
                _byteBuffer[bytesToWrite++] = (byte)(pcmSample >> 8);
            }
            
            stream.Write(_byteBuffer, 0, bytesToWrite);
            
            return bytesToWrite;
        }
        
        public override int Time()
        {
            return (int)_vorbisReader.DecodedTime.TotalMilliseconds;
        }
        
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