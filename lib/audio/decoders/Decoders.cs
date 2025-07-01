using System;
using System.Collections.Generic;
using System.IO;
using decoder_api;
using lib.audio.format;
using lib.common;
using log4net;

namespace lib.audio.decoders
{
    public class Decoders
    {
        private static Dictionary<int, Type> decoders = new Dictionary<int, Type>();
        private static ILog LOGGER = LogManager.GetLogger(typeof(Decoders));

        static Decoders()
        {
            decoders.Add(SuperAudioFormat.VORBIS.Id, typeof(VorbisDecoder));
            decoders.Add(SuperAudioFormat.MP3.Id, typeof(Mp3Decoder));
        }

        private Decoders()
        {
        }

        public static Decoder initDecoder(SuperAudioFormat format, Stream audioIn,
            float normalizationFactor)
        {
            audioIn.Seek((int)audioIn.Position, SeekOrigin.Begin);
            Type decoder = decoders[format.Id];
            if (decoder == null)
            {
                throw new DecoderException(String.Format("Unsupported audio format: {0}", format));
            }
            return decoder.GetConstructor(new []{ typeof(Stream), typeof(float)}).Invoke(new object[] { audioIn, normalizationFactor }) as Decoder;
        }
        
    }
}