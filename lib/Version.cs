using System;
using System.Diagnostics;
using System.Reflection;
using Spotify;

namespace lib
{
    public class Version
    { 
        private static String VERSION = "0.0.0"; 
        private static String OS = Environment.OSVersion.ToString().ToLower();

        static Version()
        {
            var assembly = Assembly.GetAssembly(typeof(Version));
            if (assembly != null) 
            {
                VERSION = FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion;
            }
            else VERSION = "0.0.0";
        }

        public static Platform platform()
        {
            if (OS.Contains("win")) 
            {
                return Platform.PlatformWin32X86;
            } else if (OS.Contains("mac")) 
            {
                return Platform.PlatformOsxX86;
            } else 
            {
                return Platform.PlatformLinuxX86;
            }
        }

        public static String versionNumber()
        {
            return VERSION;
        }

        public static String versionString()
        {
            return "librespot-csharp " + VERSION;
        }
        
        public static String systemInfoString()
        {
            return versionString() + "; C# " + Environment.Version + "; " + Environment.OSVersion;
        }
        
        /// <returns><see cref="BuildInfo"/> object identifying a standard client.</returns>
        public static BuildInfo standardBuildInfo()
        {
            return new BuildInfo
            {
                Product = Product.ProductClient,
                ProductFlags = { ProductFlags.ProductFlagNone },
                Platform = platform(),
                Version = 117300517
            };
        }
    }
}