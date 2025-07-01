using System;
using System.IO;
using System.Threading;
using EasyHttp.Http;
using lib.audio.decoders;
using lib.audio.format;
using lib.audio.playback;

namespace librespot
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            string vorbisInputPath = "test.ogg";

            MemoryStream ramAudio = new MemoryStream();
            using (FileStream fs = new FileStream(vorbisInputPath, FileMode.Open))
            {
                fs.CopyTo(ramAudio);
            }
            ramAudio.Position = 0;

            float normalizationFactor = 1.0f;

            // Create your VorbisDecoder instance (should implement IDecoder)
            VorbisDecoder decoder = Decoders.initDecoder(SuperAudioFormat.VORBIS, ramAudio, normalizationFactor) as VorbisDecoder;

            if (decoder == null)
            {
                Console.WriteLine("Failed to initialize VorbisDecoder.");
                return;
            }
            
            BlockingStream audioStream = new BlockingStream();

            Thread decodingThread = new Thread(() =>
            {
                while (true)
                { 
                    decoder.WriteSomeTo(audioStream);
                    audioStream.Complete();
                }
            });
            decodingThread.Priority = ThreadPriority.AboveNormal;
            decodingThread.Start();

            IPlayback playback = new Alsa();
            playback.Init(audioStream);
            playback.Play();
        }
    }
}