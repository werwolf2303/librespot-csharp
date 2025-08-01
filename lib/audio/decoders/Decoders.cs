using System;
using System.Collections.Generic;
using System.IO;
using decoder_api;
using lib.audio.format;

namespace lib.audio.decoders
{
    public class Decoders
    {
        private static Dictionary<int, Type> decoders = new Dictionary<int, Type>();

        static Decoders()
        {
            decoders.Add(SuperAudioFormat.VORBIS.Id, typeof(VorbisDecoder));
            decoders.Add(SuperAudioFormat.MP3.Id, typeof(Mp3Decoder));
        }

        private Decoders()
        {
        }

        public static Decoder InitDecoder(SuperAudioFormat format, SeekableInputStream audioIn,
            float normalizationFactor, int duration)
        {
            audioIn.Seek((int)audioIn.Position, SeekOrigin.Begin);
            Type decoder = decoders[format.Id];
            if (decoder == null)
            {
                throw new Decoder.DecoderException(String.Format("Unsupported audio format: {0}", format));
            }

            normalizationFactor = 1.0f;
            return decoder.GetConstructor(new []{ typeof(SeekableInputStream), typeof(float), typeof(int)}).Invoke(new object[] { audioIn, normalizationFactor, duration}) as Decoder;
        }
        
    }
}