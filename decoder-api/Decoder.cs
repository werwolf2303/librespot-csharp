using log4net;

namespace decoder_api
{
    public class Decoder
    {
        public static int BUFFER_SIZE = 2048;
        private static ILog LOGGER = LogManager.GetLogger(typeof(Decoder));
        protected SeekableInputStream audioIn;
        protected float normalizationFactor;
        protected int duration;
        protected volatile bool closed = false;
        protected int seekZero = 0;
        private Outpu
    }
}