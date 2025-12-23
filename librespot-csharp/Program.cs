using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using api.server;
using deps.WebSocketSharp;
using lib.audio;
using lib.audio.decoders;
using lib.audio.format;
using lib.audio.playback;
using lib.common;
using lib.core;
using lib.metadata;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using player;
using sink_api;
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
            
            //new HttpServer().EnableCors().SetMaxConnections(Int32.MaxValue).RegisterHandler(new HttpServerEndpoint((request, pathParams) =>
            //{
            //    return new HttpServerResponse(content: "<body style='background-color: black; color: white'><a>Hello World</a></body>");
            //})).Start("127.0.0.1", 8000);

            new RawHttpServer().Run();
            
            while (true)
            {
            }
        }
        
        /*public static void Main(string[] args)
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
            configuration.SetCacheEnabled(true);
            configuration.SetProxy(new WebProxy("127.0.0.1", 8080));

            Session.Builder builder = new Session.Builder(configuration.Build());

            builder.OAuth();
            Session session = builder.Create();
            
            PlayerConfiguration playercfg = new PlayerConfiguration.Builder()
                .SetOutput(PlayerConfiguration.AudioOutput.MIXER)
                .SetOutputClass("")
                .SetMixers(new Dictionary<string, Type>()
                {
                    {"PlatformLinux", typeof(Alsa)}
                })
                .Build();


            Player player = new Player(playercfg, session);
            
            Application.Run(new TestForm(player, () =>
            {
                player.Load("spotify:track:35SI5zFEhOeo4XDBMwS41S", false, false);

                player.AddToQueue("spotify:track:51FZnO9sWc2dazJneozkHp");
            }));
        }*/
    }
}