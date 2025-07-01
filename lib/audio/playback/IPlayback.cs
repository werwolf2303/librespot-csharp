using System;
using System.IO;

namespace lib.audio.playback
{
    public interface IPlayback
    {
        void Play();
        void Pause();
        void Stop();
        long Position();
        void Init(BlockingStream stream);
        void Seek(long milliseconds);
        void Dispose();
    }
}