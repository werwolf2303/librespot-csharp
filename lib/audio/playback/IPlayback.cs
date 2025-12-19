using System;
using System.IO;
using sink_api;

namespace lib.audio.playback
{
    public interface IPlayback : IDisposable
    {
        /**
         * Begins reading from audio pcm stream
         */
        void Start();
        
        /**
         * Allocates resources needed for playback
         */
        void Init(OutputAudioFormat audioFormat);
        
        /**
         * Sets the master gain
         */
        void SetVolume(float volume);
        
        /**
         * Pauses the reading from the audio pcm stream
         */
        void Suspend();
        
        /**
         * Resumes the reading from the audio pcm stream
         */
        void Resume();
        
        /**
         * Writes the pcm data to the audio device
         */
        void Write(byte[] buffer, int offset, int count);

        /**
         * Clears the output buffer
         */
        void Clear();
    }
}