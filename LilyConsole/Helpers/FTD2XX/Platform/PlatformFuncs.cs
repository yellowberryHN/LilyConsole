using System;
using System.Runtime.InteropServices;
using OperatingSystem = FTD2XX.Platform.OperatingSystem;

namespace FTD2XX.Platform
{
    public class PlatformFuncs : IPlatformFuncs
    {
        public PlatformFuncs()
        {
            #if NETFRAMEWORK
            OperatingSystem = OperatingSystem.Windows;
            #else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                OperatingSystem = OperatingSystem.Windows;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                OperatingSystem = OperatingSystem.Linux;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                OperatingSystem = OperatingSystem.OSX;
                throw new NotImplementedException("Dynamic library loading on OSX is not implemented");
            }
            else
                throw new NotImplementedException("Application is running on unknown operation system.");
            #endif
        }

        public OperatingSystem OperatingSystem { get; }

        public IntPtr LoadLibrary(string name)
        {
            switch (OperatingSystem)
            {
                case OperatingSystem.Windows:
                    return WindowsPlatformFuncs.LoadLibraryA(name);
                case OperatingSystem.Linux:
                    return LinuxPlatformFuncs.dlopen(name, LinuxPlatformFuncs.RTLD_NOW);
                case OperatingSystem.OSX:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IntPtr GetSymbol(IntPtr libraryHandle, string symbolName)
        {
            switch (OperatingSystem)
            {
                case OperatingSystem.Windows:
                    return WindowsPlatformFuncs.GetProcAddress(libraryHandle, symbolName);
                case OperatingSystem.Linux:
                    return LinuxPlatformFuncs.dlsym(libraryHandle, symbolName);
                case OperatingSystem.OSX:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public int FreeLibrary(IntPtr libraryHandle)
        {
    #if UNITY_EDITOR_LINUX
            // Avoid actually running free in the editor. We may still be writing LED data in another thread,
            // which can cause an editor segfault. TODO: see if this also affect windows, etc.
            // This will technically leak the loaded library but it should be deduplicated anyway.
            return 0;
    #endif

    #pragma warning disable CS0162 // Unreachable code detected
            // ReSharper disable HeuristicUnreachableCode
            int ret;
            switch (OperatingSystem)
            {
                case OperatingSystem.Windows:
                    ret = WindowsPlatformFuncs.FreeLibrary(libraryHandle);
                    break;
                case OperatingSystem.Linux:
                    ret = LinuxPlatformFuncs.dlclose(libraryHandle);
                    break;
                case OperatingSystem.OSX:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Since FTD2XX_NET.cs doesn't properly check the return value, do it here.
            if (ret != 0) throw new Exception($"Failed to free library {libraryHandle}");

            return ret;
            // ReSharper restore HeuristicUnreachableCode
    #pragma warning restore CS0162 // Unreachable code detected
        }


        public void Dispose()
        {
            // Don't need to do anything.
        }

        private static class LinuxPlatformFuncs
        {
            // ReSharper disable InconsistentNaming, IdentifierTypo, CommentTypo, StringLiteralTypo

            // See dlopen(3)
            public const int RTLD_NOW = 0x00002;

            // See dlopen(3)
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlopen(string filename, int flags);

            // See dlsym(3)
            [DllImport("libdl.so.2")]
            public static extern IntPtr dlsym(IntPtr handle, string symbol);

            // See dlopen(3)
            [DllImport("libdl.so.2")]
            public static extern int dlclose(IntPtr libraryHandle);

            // ReSharper restore InconsistentNaming, IdentifierTypo, CommentTypo, StringLiteralTypo
        }

        private static class WindowsPlatformFuncs
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr LoadLibraryA(string name);


            [DllImport("kernel32.dll")]
            public static extern IntPtr GetProcAddress(IntPtr libraryHandle, string symbolName);


            [DllImport("kernel32.dll")]
            public static extern int FreeLibrary(IntPtr libraryHandle);
        }
    }
}
