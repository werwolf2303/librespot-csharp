using System;

namespace sink_api
{
    public interface SinkOutput : IDisposable
    {
        /// <exception cref="SinkException"/>
        bool start(OutputAudioFormat format);

        /// <exception cref="IOException"/>
        void write(byte[] buffer, int offset, int len); 
        
        bool setVolume(float volume); 
        
        void release(); 
        
        void drain();
        
        void flush();

        void stop();
    }
}