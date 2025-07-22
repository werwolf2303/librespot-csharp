using System;
using System.Diagnostics;
using System.IO;
using lib.audio.playback;
using log4net;
using log4net.Util;
using sink_api;

namespace player.mixing.output
{
    public class PipeOutput : ISinkOutput
    {
        private static ILog LOGGER = LogManager.GetLogger(typeof(PipeOutput));
        private readonly String _path;
        private FileStream _outputStream;
        
        public PipeOutput(String path)
        {
            _path = path;
        }

        public bool Start(OutputAudioFormat format)
        {
            return false;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (_outputStream == null)
            {
                if (!File.Exists(_path))
                {
                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "/usr/bin/env",
                                Arguments = "mkfifo " + _path,
                                RedirectStandardError = false,
                                UseShellExecute = false
                            }
                        };
                        process.Start();
                        process.WaitForExit();

                        if (process.ExitCode != 0)
                            LOGGER.Warn("Failed creating pipe! exit: " + process.ExitCode);
                        else
                            LOGGER.Info("Created pipe: " + _path);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to create named pipe", ex);
                    }
                }

                _outputStream = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
            }

            _outputStream.Write(buffer, offset, count);
        }

        public bool SetVolume(float volume)
        {
            return false;
        }

        public void Suspend()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
            _outputStream?.Dispose();
        }
    }
}