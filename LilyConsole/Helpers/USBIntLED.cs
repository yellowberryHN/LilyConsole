using System;
using System.Runtime.InteropServices;
#if UNITY
using UnityEngine;
#endif

namespace LilyConsole.Helpers
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LedData
    {
        public uint unitCount;

        #if UNITY
        // Unity's Color32 is a struct of 4 bytes, so we can use it directly.
        // 480 = 60 (angle) * 8 (depth)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 480)]
        public Color32[] rgbaValues;
        #else
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 480)]
        public LightColor[] rgbaValues;
        #endif
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
        private static extern int USBIntLED_set(int data1, LedData ledData);

        public static bool Safe_USBIntLED_set(int data1, LedData ledData)
        {
            if (_dllMissing) return false;

            try
            {
                return USBIntLED_set(data1, ledData) == 0;
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
