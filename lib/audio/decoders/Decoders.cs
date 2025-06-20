using System;
using System.Collections.Generic;
using decoder_api;
using lib.audio.format;
using lib.common;
using log4net;

namespace lib.audio.decoders
{
    public class Decoders
    {
        private static Dictionary<SuperAudioFormat, List<Type>> decoders = new Dictionary<SuperAudioFormat, List<Type>>();
        private static ILog LOGGER = LogManager.GetLogger(typeof(Decoders));

        static Decoders()
        {
            //ToDo: Register decoders

        }

        private Decoders()
        {
        }

        public static Iterator<Decoder> initDecoder(SuperAudioFormat format, SeekableInputStream audioIn,
            float normalizationFactor, int duration)
        {
            List<Type> list = decoders[format];
            if (list == null) list = new List<Type>();

            int seekZero = audioIn.position();
            return new AudioIterator(seekZero, audioIn, normalizationFactor, duration, list);
        }

        private class AudioIterator : Iterator<Decoder>
        {
            private int seekZero;
            private SeekableInputStream audioIn;
            private float normalizationFactor;
            private int duration;
            private List<Type> list;
            private int currIterPosition = 0;
            
            public AudioIterator(int seekZero, SeekableInputStream audioIn, float normalizationFactor, int duration, List<Type> list)
            {
                this.seekZero = seekZero;
                this.audioIn = audioIn;
                this.normalizationFactor = normalizationFactor;
                this.duration = duration;
                this.list = list;
            }
            
            public bool HasNext()
            {
                return currIterPosition < list.Count -1;
            }

            public Decoder Next()
            {
                
                audioIn.seek(seekZero);

                Type type = list[currIterPosition];
             
                currIterPosition++;
                return Activator.CreateInstance(type, audioIn, normalizationFactor, duration) as Decoder;
            }
        }
    }
}