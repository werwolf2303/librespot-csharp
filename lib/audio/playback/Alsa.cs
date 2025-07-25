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
        private OutputAudioFormat _audioFormat;
        private byte[] _buffer;
        private BlockingStream _stream;
        private int _bytesPerFrame;
        private volatile float _volume = 1.0f;

        public void Init(OutputAudioFormat audioFormat)
        {
            _audioFormat = audioFormat;
            _stream = new BlockingStream();
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
                if (_paused)
                {
                    Thread.Sleep(100);
                    continue;
                }

                int bytesRead = _stream.Read(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                if (_volume < 1.0f)
                {
                    bool isSigned = _audioFormat.getEncoding().Equals("PCM_SIGNED");
                    bool isBigEndian = _audioFormat.isBigEndian();
                    int bytesPerSample = _audioFormat.getSampleSizeInBits() / 8;

                    for (int i = 0; i < bytesRead; i += bytesPerSample)
                    {
                        switch (_audioFormat.getSampleSizeInBits())
                        {
                            case 8:
                                if (isSigned)
                                {
                                    sbyte sample = (sbyte)_buffer[i];
                                    float newSample = sample * _volume;
                                    _buffer[i] = (byte)(sbyte)Math.Max(sbyte.MinValue,
                                        Math.Min(sbyte.MaxValue, newSample));
                                }
                                else
                                {
                                    float centeredSample = _buffer[i] - 128;
                                    float newCenteredSample = centeredSample * _volume + 128;
                                    _buffer[i] = (byte)Math.Max(byte.MinValue,
                                        Math.Min(byte.MaxValue, newCenteredSample));
                                }

                                break;

                            case 16:
                                if (isSigned)
                                {
                                    short sample = ReadInt16(_buffer, i, isBigEndian);
                                    float newSample = sample * _volume;
                                    WriteInt16(_buffer, i,
                                        (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, newSample)),
                                        isBigEndian);
                                }
                                else 
                                {
                                    ushort sample = ReadUInt16(_buffer, i, isBigEndian);
                                    float centeredSample = sample - 32768;
                                    float newCenteredSample = centeredSample * _volume + 32768;
                                    WriteUInt16(_buffer, i,
                                        (ushort)Math.Max(ushort.MinValue, Math.Min(ushort.MaxValue, newCenteredSample)),
                                        isBigEndian);
                                }

                                break;

                            case 24:
                                if (isSigned)
                                {
                                    int sample = ReadInt24(_buffer, i, isBigEndian);
                                    float newSample = sample * _volume;
                                    WriteInt24(_buffer, i, (int)Math.Max(-8388608, Math.Min(8388607, newSample)),
                                        isBigEndian);
                                }

                                break;

                            case 32:
                                if (isSigned)
                                {
                                    int sample = ReadInt32(_buffer, i, isBigEndian);
                                    float newSample = sample * _volume;
                                    WriteInt32(_buffer, i,
                                        (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, newSample)), isBigEndian);
                                }
                                else 
                                {
                                    uint sample = ReadUInt32(_buffer, i, isBigEndian);
                                    double centeredSample = sample - 2147483648.0;
                                    double newCenteredSample = centeredSample * _volume + 2147483648.0;
                                    WriteUInt32(_buffer, i,
                                        (uint)Math.Max(uint.MinValue, Math.Min(uint.MaxValue, newCenteredSample)),
                                        isBigEndian);
                                }

                                break;
                        }
                    }
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

        private static short ReadInt16(byte[] buffer, int offset, bool bigEndian)
        {
            if (bigEndian)
                return (short)(buffer[offset] << 8 | buffer[offset + 1]);
            return (short)(buffer[offset + 1] << 8 | buffer[offset]);
        }

        private static void WriteInt16(byte[] buffer, int offset, short value, bool bigEndian)
        {
            if (bigEndian)
            {
                buffer[offset] = (byte)(value >> 8);
                buffer[offset + 1] = (byte)value;
            }
            else
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
            }
        }

        private static ushort ReadUInt16(byte[] buffer, int offset, bool bigEndian) =>
            (ushort)ReadInt16(buffer, offset, bigEndian);

        private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool bigEndian) =>
            WriteInt16(buffer, offset, (short)value, bigEndian);

        private static int ReadInt24(byte[] buffer, int offset, bool bigEndian)
        {
            int value;
            if (bigEndian)
                value = buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8;
            else
                value = buffer[offset + 2] << 24 | buffer[offset + 1] << 16 | buffer[offset] << 8;
            return value >> 8;
        }

        private static void WriteInt24(byte[] buffer, int offset, int value, bool bigEndian)
        {
            if (bigEndian)
            {
                buffer[offset] = (byte)(value >> 16);
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)value;
            }
            else
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
            }
        }

        private static int ReadInt32(byte[] buffer, int offset, bool bigEndian)
        {
            if (bigEndian)
                return buffer[offset] << 24 | buffer[offset + 1] << 16 | buffer[offset + 2] << 8 | buffer[offset + 3];
            return buffer[offset + 3] << 24 | buffer[offset + 2] << 16 | buffer[offset + 1] << 8 | buffer[offset];
        }

        private static void WriteInt32(byte[] buffer, int offset, int value, bool bigEndian)
        {
            if (bigEndian)
            {
                buffer[offset] = (byte)(value >> 24);
                buffer[offset + 1] = (byte)(value >> 16);
                buffer[offset + 2] = (byte)(value >> 8);
                buffer[offset + 3] = (byte)value;
            }
            else
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
            }
        }

        private static uint ReadUInt32(byte[] buffer, int offset, bool bigEndian) =>
            (uint)ReadInt32(buffer, offset, bigEndian);

        private static void WriteUInt32(byte[] buffer, int offset, uint value, bool bigEndian) =>
            WriteInt32(buffer, offset, (int)value, bigEndian);


        void CreatePlaybackThread()
        {
            _playbackThread = new Thread(Run);
            _playbackThread.Start();
        }

        public void Start()
        {
            if (_stopped)
            {
                if (_stream.CanSeek) _stream.Seek(0, SeekOrigin.Begin);
                _stopped = false;
                CreatePlaybackThread();
            }

            _paused = false;
        }

        public void SetVolume(float volume)
        {
            if (volume < 0.0f) _volume = 0.0f;
            else if (volume > 1.0f) _volume = 1.0f;
            else _volume = volume;
        }

        public void Suspend()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public void Dispose()
        {
            _stopped = true;
            _playbackThread.Join();
            if (_pcmHandle != IntPtr.Zero)
            {
                AlsaWrapper.snd_pcm_close(_pcmHandle);
                _pcmHandle = IntPtr.Zero;
            }
        }
    }
}