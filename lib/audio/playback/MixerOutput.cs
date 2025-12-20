using System;
using lib.common;
using log4net;
using sink_api;
using Spotify;

namespace lib.audio.playback
{
    public class MixerOutput : ISinkOutput
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(MixerOutput));
        private IPlayback _playback;
        private bool _logAvailableMixers;
        private float _lastVolume = -1;
        private bool _running = false;
        
        public MixerOutput(String[] mixerSearchKeywords, bool logAvailableMixers, String audioOutputMethod, String audioOutputClass)
        {
            switch (audioOutputMethod)
            {
                case "AUTO":
                    switch (Version.platform())
                    {
                        case Platform.PlatformLinuxX86:
                            _playback = new Alsa();
                            break;
                        case Platform.PlatformWin32X86:
                            _playback = new Dummy();
                            break;
                        default:
                            throw new NotImplementedException("Playback not implemented for: " + Version.platform());
                    }
                    break;
                case "ALSA":
                    _playback = new Alsa();
                    break;
                case "CUSTOM":
                    if (audioOutputClass == null)
                        throw new InvalidOperationException(
                            "AudioOutputClass must be set when using custom audio output");
                    Type type = Utils.FindType(audioOutputClass);
                    if (type == null)
                        throw new InvalidOperationException("Couldn't find audio output class in: " + audioOutputClass);
                    _playback = type.GetConstructor(new Type[] { }).Invoke(null) as IPlayback;
                    break;
            }
        }
        
        public bool Start(OutputAudioFormat format)
        {
            _playback.Init(format);
            return true;
        }

        public void Write(byte[] buffer, int offset, int len)
        {
            if (!_running)
            {
                _playback.Start();
                _running = true;
            }
            _playback.Write(buffer, offset, len);
        }

        public bool SetVolume(float volume)
        {
            _playback.SetVolume(volume);
            return true;
        }

        public void Suspend()
        {
            _playback.Suspend();
        }

        public void Resume()
        {
            _playback.Resume();
        }

        public void Dispose()
        {
            _playback.Dispose();
        }

        public void Clear()
        {
            _playback.Clear();
        }

        public void Flush()
        {
            _playback.Flush();
        }
    }
}