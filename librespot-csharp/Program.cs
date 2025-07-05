using System;
using System.IO;
using System.Net;
using System.Threading;
using deps.WebSocketSharp;
using lib.audio;
using lib.audio.decoders;
using lib.audio.format;
using lib.audio.playback;
using lib.core;
using lib.metadata;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using spotify.clienttoken.http.v0;
using Logger = log4net.Repository.Hierarchy.Logger;

namespace librespot
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository();
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Name = "librespot-csharp"; 
            PatternLayout consoleLayout = new PatternLayout();
            consoleLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
            consoleLayout.ActivateOptions(); 
            consoleAppender.Layout = consoleLayout;
            consoleAppender.ActivateOptions();
            Logger rootLogger = hierarchy.Root;
            rootLogger.Level = Level.All;
            rootLogger.AddAppender(consoleAppender);
            hierarchy.Configured = true;

            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                return true;
            };

            Session.Configuration.Builder configuration = new Session.Configuration.Builder();

            configuration.SetStoreCredentials(true);
            configuration.SetStoredCredentialsFile("credentials.json");

            Session.Builder builder = new Session.Builder(configuration.Build());

            builder.OAuth();
            Session session = builder.Create();
            
            session.GetClient().Proxy = new WebProxy("127.0.0.1", 8090);
            
            IDecodedAudioStream audioStream = session.GetContentFeeder().Load(
                TrackId.FromUri("spotify:track:6AxCr5G75R5rqyNCYWVpTo"),
                new VorbisOnlyAudioQuality(AudioQuality.VERY_HIGH),
                true,
                null
            ).In;

            BlockingStream stream = new BlockingStream();
            VorbisDecoder decoder = Decoders.InitDecoder(SuperAudioFormat.VORBIS, audioStream.Stream(), 0.0f) as VorbisDecoder;

            Thread thread = new Thread(() =>
            {
                decoder.WriteSomeTo(stream);
            });
            thread.Start();

            IPlayback playback = new Alsa();
            playback.Init(stream);
            playback.Play();
        }
    }
}