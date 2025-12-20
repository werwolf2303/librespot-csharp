using System;

namespace sink_api
{
    public interface ISinkOutput : IDisposable
    {
        bool Start(OutputAudioFormat format);

        void Write(byte[] buffer, int offset, int len);

        bool SetVolume(float volume);

        void Suspend();

        void Resume();

        void Clear();

        void Flush();
    }
}