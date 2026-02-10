using System;
using System.Runtime.InteropServices;

namespace LilyConsole.Helpers
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LedData
    {
        public uint unitCount;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 480)]
        public LightColor[] rgbaValues;
        
        public static LedData blank = new LedData() { unitCount = 480, rgbaValues = new LightColor[480] };

        public LedData(LightColor[] data)
        {
            unitCount = (uint)data.Length;
            rgbaValues = data;
        }
    }

    // ReSharper disable once InconsistentNaming
    public static class USBIntLED
    {
        private static bool _dllMissing;

        [DllImport("USBIntLED")]
        private static extern int USBIntLED_Init();

        public static bool Safe_USBIntLED_Init()
        {
            if (_dllMissing) return false;

            try
            {
                return USBIntLED_Init() != 0;
            }
            catch (DllNotFoundException)
            {
                _dllMissing = true;
                return false;
            }
            catch (BadImageFormatException)
            {
                _dllMissing = true;
                return false;
            }
        }
        
        [DllImport("USBIntLED")]
        private static extern int USBIntLED_Terminate();

        public static bool Safe_USBIntLED_Terminate()
        {
            if (_dllMissing) return false;

            try
            {
                return USBIntLED_Terminate() != 0;
            }
            catch (DllNotFoundException)
            {
                _dllMissing = true;
                return false;
            }
            catch (BadImageFormatException)
            {
                _dllMissing = true;
                return false;
            }
        }

        [DllImport("USBIntLED")]
        private static extern int USBIntLED_set(int offset, LedData ledData);

        public static bool Safe_USBIntLED_set(int offset, LedData ledData)
        {
            if (_dllMissing) return false;

            try
            {
                return USBIntLED_set(offset, ledData) == 0;
            }
            catch (DllNotFoundException)
            {
                _dllMissing = true;
                return false;
            }
            catch (BadImageFormatException)
            {
                _dllMissing = true;
                return false;
            }
        }
        
        [DllImport("USBIntLED")]
        private static extern int USBIntLED_getVersion();

        public static int Safe_USBIntLED_getVersion()
        {
            if (_dllMissing) return -1;

            try
            {
                return USBIntLED_getVersion();
            }
            catch (DllNotFoundException)
            {
                _dllMissing = true;
                return -1;
            }
            catch (BadImageFormatException)
            {
                _dllMissing = true;
                return -1;
            }
        }
    }
}
