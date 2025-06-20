using System;
using EasyHttp.Http;
using lib.audio.decoders;
using lib.common;
using lib.core;
using lib.mercury;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Tarczynski.NtpDateTime;
using Version = lib.Version;

namespace librespot {
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

            Session.Configuration.Builder configuration = new Session.Configuration.Builder();

            configuration.setStoreCredentials(true);
            configuration.setStoredCredentialsFile("credentials.json");
            
            Session.Builder builder = new Session.Builder(configuration.build());
            
            builder.oauth();
            builder.create();
        }
    }
}