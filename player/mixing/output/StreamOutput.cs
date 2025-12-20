using System.IO;
using sink_api;

namespace player.mixing.output
{
    public class StreamOutput : ISinkOutput
    {
        private Stream _outputStream;
        private bool _close;
        
        public StreamOutput(Stream outputStream, bool close) : base()
        {
            _outputStream = outputStream;
            _close = close;
        }
        
        public void Dispose()
        {
            if (_close) _outputStream.Dispose();
        }

        public bool Start(OutputAudioFormat format)
        {
            return false;
        }

        public void Write(byte[] buffer, int offset, int len)
        {
            _outputStream.Write(buffer, offset, len);
        }

        public bool SetVolume(float volume)
        {
            return false;
        }

        public void Resume()
        {
        }

        public void Suspend()
        {
        }

        public void Clear()
        {
        }

        public void Flush()
        {
        }
    }
}