using System;
using System.Threading;
using decoder_api;
using NAudio.Wave;
using sink_api;

namespace lib.audio.playback
{
    public class WindowsPlayback : IPlayback
    {
        private IWavePlayer _waveOut;
        private BufferedWaveProvider _bufferedProvider;
        private OutputAudioFormat _audioFormat;
        private int _bytesPerFrame;
        private volatile float _volume = 1.0f;
        private volatile bool _stopped;
        private volatile bool _paused;
        private BlockingStream _mainBuffer;
        private byte[] _buffer;
        private readonly object _writeLock = new object();
        private readonly object _pauseLock = new object();
        private Thread _playbackThread;

        public void Init(OutputAudioFormat audioFormat)
        {
            _audioFormat = audioFormat;
            _bytesPerFrame = (audioFormat.getSampleSizeInBits() / 8) * audioFormat.getChannels();
            _buffer = new byte[Decoder.BUFFER_SIZE * _bytesPerFrame];
            _mainBuffer = new BlockingStream(4096 * _bytesPerFrame);

            var waveFormat = new WaveFormat((int) _audioFormat.getSampleRate(), _audioFormat.getSampleSizeInBits(), _audioFormat.getChannels());
            _bufferedProvider = new BufferedWaveProvider(waveFormat)
            {
                DiscardOnBufferOverflow = true
            };
            
            _waveOut = new WaveOut();
            _waveOut.Init(_bufferedProvider);
            _stopped = true;
        }

        public void Start()
        {
            if (_waveOut == null) throw new InvalidOperationException("WindowsPlayback not initialized");
            if (!_stopped) return;
            _stopped = false;
            _waveOut.Play();
            _playbackThread = new Thread(Run) { Name = "WindowsPlayback-thread" };
            _playbackThread.Start();
        }

        private void Run()
        {
            while (true)
            {
                if (_stopped) break;

                if (_paused)
                {
                    lock (_pauseLock)
                    {
                        Monitor.Wait(_pauseLock);
                    }
                }

                int bytesRead = _mainBuffer.Read(_buffer, 0, _buffer.Length);
                if (bytesRead == 0)
                {
                    lock (_writeLock) { Monitor.PulseAll(_writeLock); }
                    Thread.Sleep(10);
                    continue;
                }

                if (_volume < 1.0f)
                {
                    ApplyVolumeInPlace(_buffer, bytesRead);
                }

                _bufferedProvider.AddSamples(_buffer, 0, bytesRead);
            }
        }

        private void ApplyVolumeInPlace(byte[] buffer, int count)
        {
            bool isSigned = _audioFormat.getEncoding().Equals("PCM_SIGNED");
            bool isBigEndian = _audioFormat.isBigEndian();
            int bytesPerSample = _audioFormat.getSampleSizeInBits() / 8;

            for (int i = 0; i < count; i += bytesPerSample)
            {
                switch (_audioFormat.getSampleSizeInBits())
                {
                    case 8:
                        if (isSigned)
                        {
                            sbyte sample = (sbyte)buffer[i];
                            float newSample = sample * _volume;
                            buffer[i] = (byte)(sbyte)Math.Max(sbyte.MinValue, Math.Min(sbyte.MaxValue, newSample));
                        }
                        else
                        {
                            float centeredSample = buffer[i] - 128;
                            float newCenteredSample = centeredSample * _volume + 128;
                            buffer[i] = (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, newCenteredSample));
                        }
                        break;

                    case 16:
                        if (isSigned)
                        {
                            short sample = ReadInt16(buffer, i, isBigEndian);
                            float newSample = sample * _volume;
                            WriteInt16(buffer, i, (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, newSample)), isBigEndian);
                        }
                        else
                        {
                            ushort sample = ReadUInt16(buffer, i, isBigEndian);
                            float centeredSample = sample - 32768;
                            float newCenteredSample = centeredSample * _volume + 32768;
                            WriteUInt16(buffer, i, (ushort)Math.Max(ushort.MinValue, Math.Min(ushort.MaxValue, newCenteredSample)), isBigEndian);
                        }
                        break;

                    case 24:
                        if (isSigned)
                        {
                            int sample = ReadInt24(buffer, i, isBigEndian);
                            float newSample = sample * _volume;
                            WriteInt24(buffer, i, (int)Math.Max(-8388608, Math.Min(8388607, newSample)), isBigEndian);
                        }
                        break;

                    case 32:
                        if (isSigned)
                        {
                            int sample = ReadInt32(buffer, i, isBigEndian);
                            float newSample = sample * _volume;
                            WriteInt32(buffer, i, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, newSample)), isBigEndian);
                        }
                        else
                        {
                            uint sample = ReadUInt32(buffer, i, isBigEndian);
                            double centeredSample = sample - 2147483648.0;
                            double newCenteredSample = centeredSample * _volume + 2147483648.0;
                            WriteUInt32(buffer, i, (uint)Math.Max(uint.MinValue, Math.Min(uint.MaxValue, newCenteredSample)), isBigEndian);
                        }
                        break;
                }
            }
        }

        public void SetVolume(float volume)
        {
            _volume = Math.Max(0.0f, Math.Min(1.0f, volume));
            if (_waveOut is WaveOut wo)
            {
                try { wo.Volume = _volume; } catch { }
            }
        }

        public void Suspend()
        {
            if (_paused) return;
            _paused = true;
            _waveOut?.Pause();
        }

        public void Resume()
        {
            if (!_paused) return;
            _paused = false;
            _waveOut?.Play();
            lock (_pauseLock) { Monitor.PulseAll(_pauseLock); }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_stopped) return;
            _mainBuffer.Write(buffer, offset, count);
        }

        public void Clear()
        {
            _bufferedProvider?.ClearBuffer();
        }

        public void Flush()
        {
            lock (_writeLock) { Monitor.Wait(_writeLock, 100); }
        }

        public void Dispose()
        {
            _stopped = true;
            try { _waveOut?.Stop(); } catch { }
            if (_playbackThread != null && _playbackThread.IsAlive)
            {
                _playbackThread.Join(2000);
            }
            _waveOut?.Dispose();
        }

        private static short ReadInt16(byte[] buffer, int offset, bool bigEndian)
        {
            if (bigEndian) return (short)(buffer[offset] << 8 | buffer[offset + 1]);
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

        private static ushort ReadUInt16(byte[] buffer, int offset, bool bigEndian) => (ushort)ReadInt16(buffer, offset, bigEndian);
        private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool bigEndian) => WriteInt16(buffer, offset, (short)value, bigEndian);

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

        private static uint ReadUInt32(byte[] buffer, int offset, bool bigEndian) => (uint)ReadInt32(buffer, offset, bigEndian);
        private static void WriteUInt32(byte[] buffer, int offset, uint value, bool bigEndian) => WriteInt32(buffer, offset, (int)value, bigEndian);
    }
}