using System.IO;
using decoder_api;

namespace lib.audio.decoders
{
  public class Mp3Decoder : Decoder
    {

        public Mp3Decoder(SeekableInputStream audioIn, float normalizationFactor, int duration)
            : base(audioIn, normalizationFactor, duration)
        {
            
        }

        protected override int ReadInternal(Stream stream)
        {
            return -1;
        }

        public override int Time()
        {
            return -1;
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
                
            }
            base.Dispose(disposing);
        }
    }
}