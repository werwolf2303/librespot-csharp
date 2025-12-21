using System;
using System.Diagnostics;
using System.Reflection;
using Spotify;

namespace lib
{
    public class Version
    { 
        private static String VERSION; 
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
            String os;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32Windows:
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.WinCE:
                    os = "Windows";
                    break;
                case PlatformID.Unix:
                    os = "Linux";
                    break;
                case PlatformID.MacOSX:
                    os = "MacOSX";
                    break;
                default:
                    os = "Unknown";
                    break;
            }
            return versionString() + "; C# " + Environment.Version + "; " + os;
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