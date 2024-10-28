// ReSharper disable All

using System;
using System.Collections.Generic;
using System.Text;

namespace FTD2XX.Platform
{
    public interface IPlatformFuncs: IDisposable
    {

        IntPtr LoadLibrary(string name);

        IntPtr GetSymbol(IntPtr libraryHandle, string symbolName);

        int FreeLibrary(IntPtr libraryHandle);

        OperatingSystem OperatingSystem { get; }

    }

    public enum OperatingSystem { Windows, Linux, OSX }

}
