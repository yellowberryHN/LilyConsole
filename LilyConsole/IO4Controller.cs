using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using HidSharp;

namespace LilyConsole
{
    public class IO4Controller
    {
        private HidDevice device;
        
        private HidStream stream;
        
        private byte[] readBuffer = new byte[64];
        private byte[] writeBuffer = new byte[64];
        
        public IO4Report lastReport { get; private set; }
        
        public IO4ButtonState buttonState { get; private set; }
        
        public IO4Controller()
        {
            device = DeviceList.Local.GetHidDeviceOrNull(0x0CA3, 0x0021);
            if (device == null) throw new InvalidOperationException("IO4 HID device not found");
            CheckProductName(device.GetProductName());
        }

        public void Initialize()
        {
            stream = device.Open();
            writeBuffer[0] = 16;
        }

        public void Close()
        {
            ClearColor();
            stream.Close();
        }
        
        public void SetColor(LightColor color, byte brightness = 0)
        {
            Array.Clear(writeBuffer, 1, writeBuffer.Length - 1);
            
            writeBuffer[1] = 0x41; // set unique output
            
            var unique = new byte[8] { 0b00111000, brightness, 0, 0, color.r, color.g, color.b, 0 };
            
            Array.Copy(unique, 0, writeBuffer, 2, 8);
            
            stream.Write(writeBuffer);
        }

        public void ClearColor()
        {
            Array.Clear(writeBuffer, 1, writeBuffer.Length - 1);
            
            writeBuffer[1] = 0x41; // set unique output
            
            // everything else is set to 0;
            
            stream.Write(writeBuffer);
        }

        public void ClearBuffer()
        {
            // there is no way to clear, and we will not be able to catch up.
            stream.Close();
            stream = device.Open();
        }

        /// <summary>
        /// Asks the IO4 board to return the current status.
        /// <b><i>Expects to be called every 8ms.</i></b>
        /// If you can't call it that fast, set <paramref name="discard"/> to the number of packets to skip.
        /// For example, if you can only call it every 32ms, set <paramref name="discard"/> to 3.
        ///
        /// This is a bad approach, however, ideally we'd overlay the discarded packets and merge them,
        /// so we can react to the data, but that's slow. Maybe I'll try that later...
        /// </summary>
        /// <param name="discard">How many packets to ignore.</param>
        /// <returns>The </returns>
        /// <exception cref="InvalidDataException">
        /// Thrown if there aren't enough bytes to read, this should never happen.
        /// </exception>
        public IO4Report Poll(int discard = 0)
        {
            for (var i = 0; i < discard + 1; i++)
            {
                if(stream.Read(readBuffer, 0, readBuffer.Length) != readBuffer.Length)
                    throw new InvalidDataException("Not enough available bytes to read");
            }
            
            if (readBuffer[0] == 1) lastReport = IO4Report.Build(readBuffer);
            else throw new InvalidDataException($"Unexpected Report ID 0x{readBuffer[0]:X}");
            
            buttonState = (IO4ButtonState)lastReport.buttons[0];
            
            return lastReport;
        }

        /// <summary>
        /// Debugging method. 
        /// </summary>
        public void ButtonLights()
        {
            switch (buttonState)
            {
                case IO4ButtonState.VolumeDown:
                    SetColor(LightColor.Red);
                    break;
                case IO4ButtonState.VolumeUp:
                    SetColor(LightColor.Blue);
                    break;
                case IO4ButtonState.Test:
                    SetColor(LightColor.Green);
                    break;
                case IO4ButtonState.Service:
                    SetColor(LightColor.White);
                    break;
                default:
                    ClearColor();
                    break;
            }
        }

        public void DebugInfo()
        {
            Console.WriteLine(lastReport);
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 7);
        }

        private static void CheckProductName(string name)
        {
            Console.WriteLine(name);
            var parts = name.Split(';');
        }
    }

    [Flags]
    public enum IO4ButtonState
    {
        VolumeDown = 1 << 0,
        VolumeUp = 1 << 1,
        Service = 1 << 6,
        Test = 1 << 9
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IO4Report
    {
        public readonly byte reportId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public readonly ushort[] adc;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public readonly ushort[] rotary;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public readonly ushort[] coin;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public readonly ushort[] buttons;
        
        public readonly BoardStatus boardStatus;
        public readonly UsbStatus usbStatus;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 29)]
        public readonly byte[] unique;
        
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public readonly struct BoardStatus
        {
            private readonly byte value;

            public byte resetReason => (byte)(value & 0x0F);
            public bool timeoutSet => (value & 0x10) != 0;
            public bool sampleCountSet => (value & 0x20) != 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public readonly struct UsbStatus
        {
            private readonly byte value;

            public bool timeoutOccurred => (value & 0x04) != 0;
        }

        public static IO4Report Build(byte[] raw)
        {
            if (raw.Length != Marshal.SizeOf(typeof(IO4Report)))
            {
                throw new ArgumentException($"Expected data size: {Marshal.SizeOf(typeof(IO4Report))}, but got {raw.Length}");
            }
            
            var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                return Marshal.PtrToStructure<IO4Report>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append($"Buttons:\n");
            
            foreach (var button in buttons)
            {
                var binaryChar = Convert.ToString(button, 2).PadLeft(16, '0').ToCharArray();
                Array.Reverse(binaryChar);
                sb.Append(string.Join(" ", (new string(binaryChar)).ToCharArray()) + "\n");
            }
            
            sb.Append("Timeout Set?: " + boardStatus.timeoutSet);
            sb.Append("\n");
            sb.Append("Sample Count Set?: " + boardStatus.sampleCountSet);
            sb.Append("\n");
            sb.Append("Timeout Occurred?: " + usbStatus.timeoutOccurred);
            sb.Append("\n");
            sb.Append("Reset Reason: " + boardStatus.resetReason);
            
            return sb.ToString();
        }
    }
}