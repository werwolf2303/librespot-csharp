using lib.core;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

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

            Session.Configuration.Builder configuration = new Session.Configuration.Builder();

            configuration.SetStoreCredentials(true);
            configuration.SetStoredCredentialsFile("credentials.json");

            Session.Builder builder = new Session.Builder(configuration.Build());

            builder.OAuth();
            builder.Create();
        }
    }
}