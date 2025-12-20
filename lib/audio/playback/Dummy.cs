using sink_api;

namespace lib.audio.playback
{
    public class Dummy : IPlayback
    {
        public void Dispose()
        {
        }

        public void Start()
        {
        }

        public void Init(OutputAudioFormat audioFormat)
        {
        }

        public void SetVolume(float volume)
        {
        }

        public void Suspend()
        {
        }

        public void Resume()
        {
        }

        public void Write(byte[] buffer, int offset, int count)
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