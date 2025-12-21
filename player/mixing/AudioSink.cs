using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using decoder_api;
using lib.audio.playback;
using lib.common;
using Newtonsoft.Json.Linq;
using player.mixing.output;
using sink_api;

namespace player.mixing
{
    public class AudioSink : IDisposable
    {
        private ISinkOutput _output;
        private MixingLine _mixing = new MixingLine();
        private Thread _thread;
        private volatile bool _closed = false;
        private volatile bool _paused = true;

        public AudioSink(PlayerConfiguration conf)
        {
            switch (conf._output)
            {
                case PlayerConfiguration.AudioOutput.MIXER:
                    _output = new MixerOutput(conf._mixerSearchKeywords, conf._logAvailableMixers,
                        conf._audioOutputMethod.ToString(), conf._audioOutputClass, conf._mixers);
                    break;
                case PlayerConfiguration.AudioOutput.PIPE:
                    if (conf._outputPipe == null)
                        throw new InvalidOperationException("Pipe file not configured!");
                    
                    _output = new PipeOutput(conf._outputPipe);
                    break;
                case PlayerConfiguration.AudioOutput.STDOUT:
                    _output = new StreamOutput(Console.OpenStandardOutput(), false);
                    break;
                case PlayerConfiguration.AudioOutput.CUSTOM:
                    if (string.IsNullOrEmpty(conf._outputClass))
                        throw new InvalidOperationException("Custom output sink class not configured!");

                    Object[] objParams = conf._outputClassParams;
                    if (objParams == null) objParams = new object[0];
                    _output = InitCustomOutputSink(conf._outputClass, objParams);
                    break;
                default:
                    throw new InvalidOperationException("Unknown output: " + conf._output);
            }

            if (conf._bypassSinkVolume) SetVolume(Player.VOLUME_MAX);
            else SetVolume(conf._initialVolume);
        }
        
        private ISinkOutput InitCustomOutputSink(string className, Object[] parameters)
        {
            Type type = Utils.FindType(className);
            List<Type> types = new List<Type>();
            foreach (Object obj in parameters)
                types.Add(obj.GetType());
            
            return (ISinkOutput) type.GetConstructor(types.ToArray()).Invoke(parameters);
        }

        public void ClearOutputs()
        {
            _mixing.FirstOut().Clear();
            _mixing.SecondOut().Clear();
        }

        public void Clear()
        {
            _output.Clear();
        }

        public MixingLine.MixingOutput SomeOutput()
        {
            return _mixing.SomeOut(this);
        }

        public void Resume()
        {
            _paused = false;
            if (_thread == null) _thread = new Thread(Run);
            if (!_thread.IsAlive) _thread.Start();
            _output.Resume();
        }

        public void Pause()
        {
            _paused = true;
            _output.Suspend();
        }

        public void SetVolume(int volume)
        {
            if (volume < 0 || volume > Player.VOLUME_MAX)
                throw new InvalidOperationException("Invalid volume: " + volume);
            
            float volumeNorm = ((float) volume / Player.VOLUME_MAX);
            if (_output.SetVolume(volumeNorm)) _mixing.SetGlobalGain(1);
            else _mixing.SetGlobalGain(volumeNorm);
        }

        public void Dispose()
        {
            _closed = true;
            _paused = true;
            _thread.Interrupt();
            
            ClearOutputs();
        }

        private void Run()
        {
            byte[] buffer = new byte[Decoder.BUFFER_SIZE * 2];

            bool started = false;
            while (!_closed)
            {
                if (_paused)
                {
                    break;
                }
                else
                {
                    OutputAudioFormat format = _mixing.GetFormat();
                    if (!started || _mixing._switchFormat)
                    {
                        if (format != null) started = _output.Start(format);
                        _mixing._switchFormat = false;
                    } 
                    
                    int count = _mixing.Read(buffer, 0, buffer.Length);
                    _output.Write(buffer, 0, count);
                }
            }
            
            if (!_paused) _output.Dispose();
        }

        public void Flush()
        {
            _output.Flush();
        }
    }
}