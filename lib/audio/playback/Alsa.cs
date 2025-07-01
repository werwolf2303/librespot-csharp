using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using sink_api;

namespace lib.audio.playback
{
    class AlsaWrapper
    {
        public enum SndPcmStream
        {
            Playback = 0,
            Capture = 1,
        }

        public enum SndPcmAccess
        {
            RwInterleaved = 3
        }

        [DllImport("libasound.so.2")]
        public static extern IntPtr snd_strerror(int errnum);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_close(IntPtr pcm);

        [DllImport("libasound.so.2")]
        public static extern long snd_pcm_hw_params_sizeof();

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params_any(IntPtr pcm, IntPtr pcm_hw_params);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params_set_access(IntPtr pcm, IntPtr pcm_hw_params, SndPcmAccess access);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params_set_format(IntPtr pcm, IntPtr pcm_hw_params, int format);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params_set_rate_near(IntPtr pcm, IntPtr pcm_hw_params, ref uint rate,
            IntPtr dir);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params_set_channels(IntPtr pcm, IntPtr pcm_hw_params, uint channels);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_hw_params(IntPtr pcm, IntPtr pcm_hw_params);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_prepare(IntPtr pcm);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_writei(IntPtr pcm, byte[] buffer, uint sizeInFrames);

        [DllImport("libasound.so.2")]
        public static extern int snd_pcm_recover(IntPtr pcm, int err, int silent);

        public class AlsaException : Exception
        {
            public AlsaException()
            {
            }

            public AlsaException(string message) : base(message)
            {
            }
        }
    }

    public class Alsa : IPlayback
    {
        private IntPtr _pcmHandle;
        private Thread _playbackThread;
        private volatile bool _paused;
        private volatile bool _stopped;
        private readonly OutputAudioFormat _audioFormat = OutputAudioFormat.DEFAULT_FORMAT;
        private byte[] _buffer;
        private BlockingStream _stream;
        private int _bytesPerFrame;

        public void Init(BlockingStream stream)
        {
            _stream = stream;
            _paused = false;
            _stopped = true;

            _bytesPerFrame = (_audioFormat.getSampleSizeInBits() / 8) * _audioFormat.getChannels();
            _buffer = new byte[4096 * _bytesPerFrame];

            CheckAlsaError(AlsaWrapper.snd_pcm_open(out _pcmHandle, "default", 0, 0), "snd_pcm_open");

            long hwParamsSize = AlsaWrapper.snd_pcm_hw_params_sizeof();
            IntPtr hwParams = Marshal.AllocHGlobal((int)hwParamsSize);

            CheckAlsaError(AlsaWrapper.snd_pcm_hw_params_any(_pcmHandle, hwParams), "snd_pcm_hw_params_any");

            CheckAlsaError(
                AlsaWrapper.snd_pcm_hw_params_set_access(_pcmHandle, hwParams, AlsaWrapper.SndPcmAccess.RwInterleaved),
                "snd_pcm_hw_params_set_access");

            CheckAlsaError(AlsaWrapper.snd_pcm_hw_params_set_format(_pcmHandle, hwParams, 2),
                "snd_pcm_hw_params_set_format");

            uint rate = (uint)_audioFormat.getSampleRate();
            CheckAlsaError(AlsaWrapper.snd_pcm_hw_params_set_rate_near(_pcmHandle, hwParams, ref rate, IntPtr.Zero),
                "snd_pcm_hw_params_set_rate_near");

            CheckAlsaError(
                AlsaWrapper.snd_pcm_hw_params_set_channels(_pcmHandle, hwParams, (uint)_audioFormat.getChannels()),
                "snd_pcm_hw_params_set_channels");

            CheckAlsaError(AlsaWrapper.snd_pcm_hw_params(_pcmHandle, hwParams), "snd_pcm_hw_params");

            if (hwParams != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(hwParams);
            }
        }

        private void CheckAlsaError(int err, string functionName)
        {
            if (err < 0)
            {
                string errorMsg = Marshal.PtrToStringAnsi(AlsaWrapper.snd_strerror(err));
                throw new AlsaWrapper.AlsaException($"ALSA Error in {functionName}: {errorMsg} (Code: {err})");
            }
        }

        void Run()
        {
            while (true)
            {
                if (_stopped) break;
                if (_paused) continue;
                
                int bytesRead = _stream.Read(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                {
                    continue;
                }

                uint framesToWrite = (uint)(bytesRead / _bytesPerFrame);


                int result = AlsaWrapper.snd_pcm_writei(_pcmHandle, _buffer, framesToWrite);
                if (result < 0)
                {
                    _stopped = true;
                    throw new AlsaWrapper.AlsaException();
                }
            }
        }

        void CreatePlaybackThread()
        {
            _playbackThread = new Thread(Run);
            _playbackThread.Start();
        }

        public void Play()
        {
            if (_stopped)
            {
                if (_stream.CanSeek) _stream.Seek(0, SeekOrigin.Begin);
                _stopped = false;
                CreatePlaybackThread();
            }

            _paused = false;
        }

        public void Pause()
        {
            _paused = true;
        }

        public void Stop()
        {
            _stopped = true;
        }

        public long Position()
        {
            throw new System.NotImplementedException();
        }

        public void Seek(long milliseconds)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            _stopped = true;
            _playbackThread.Join();
            AlsaWrapper.snd_pcm_close(_pcmHandle);
        }
    }
}