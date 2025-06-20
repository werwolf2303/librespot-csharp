using System.IO;
using decoder_api;

namespace lib.audio.decoders
{
    public class Mp3Decoder : Decoder
    {
        public Mp3Decoder(SeekableInputStream audioIn, float normalizationFactor, int duration) : base(audioIn, normalizationFactor, duration)
        {
        }

        protected override int readInternal(Stream stream)
        {
            throw new System.NotImplementedException();
        }

        public override int time()
        {
            throw new System.NotImplementedException();
        }

        public override void Close()
        {
            throw new System.NotImplementedException(); 
        }
    }
}