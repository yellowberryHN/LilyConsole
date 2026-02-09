using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using LilyConsole.Helpers;

namespace LilyConsole
{
    #region Touch
    
    /// <summary>
    /// Information for a currently active segment.
    /// </summary>
    public struct ActiveSegment
    {
        /// <summary>
        /// Row number, from closest to screen to furthest.
        /// </summary>
        /// <remarks>Range: 0-3</remarks>
        public byte x { get; }

        /// <summary>
        /// Column number, from the top left, around the ring to the top right.
        /// </summary>
        /// <remarks>Range: 0-59</remarks>
        public byte y { get; }

        public ActiveSegment(byte x, byte y)
        {
            this.x = x;
            this.y = y;
        }

        public override string ToString()
        {
            return $"[{x},{y:D2}]";
        }
    }
    
    /// send the unknown ones at your own peril. research ongoing...
    public enum TouchCommandType
    {
        NextWrite = 0x20,
        Unknown6F = 0x6F,
        Unknown71 = 0x71,
        NextRead = 0x72,
        BeginWrite = 0x77,
        TouchData = 0x81,
        TouchDataAnalog = 0x82,
        Unknown90 = 0x90,
        Unknown91 = 0x91,
        Unknown93 = 0x93,
        SetThresholds = 0x94,
        Unknown9A = 0x9A,
        Unknown9B = 0x9B,
        Unknown9C = 0x9C,
        StartAutoScanGap = 0x9D,
        ProtoAutoScan = 0x9E,
        StartAutoScanChatter = 0x9F,
        GetSyncBoardVersion = 0xA0,
        UnknownA1 = 0xA1,
        GetActiveUnitBoards = 0xA2,
        UnknownRead = 0xA3,
        GetUnitBoardVersion = 0xA8,
        UnknownA9 = 0xA9,
        UnknownBC = 0xBC,
        StartAutoScanAnalog = 0xBD,
        UnknownC0 = 0xC0,
        UnknownC1 = 0xC1,
        UnknownC8 = 0xC8,
        StartAutoScan = 0xC9,
    }
    
    #endregion

    #region Lights
    
    public struct LightColor : IEquatable<LightColor>
    {
        public static LightColor Red => new LightColor(255, 0, 0);
        public static LightColor Green => new LightColor(0, 255, 0);
        public static LightColor Blue => new LightColor(0, 0, 255);
        public static LightColor White => new LightColor(255, 255, 255);
        public static LightColor Black => new LightColor(0, 0, 0);
        public static LightColor Off => new LightColor();
        
        /// <summary>
        /// The color, stored as ABGR. When marshalled, it gets flipped to RGBA
        /// </summary>
        /// <remarks>This is probably way unnecessary.</remarks>
        public readonly uint value;
        
        public byte r => (byte)value;
        public byte g => (byte)(value >> 8);
        public byte b => (byte)(value >> 16);
        public byte a => (byte)(value >> 24);

        public LightColor(byte r, byte g, byte b, byte a = 0xFF)
        {
            value = (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        internal LightColor(uint value)
        {
            this.value = value;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public bool Equals(LightColor other)
        {
            return value == other.value;
        }
        
        public override bool Equals(object obj)
        {
            return obj is LightColor other && Equals(other);
        }

        public static bool operator ==(LightColor left, LightColor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(LightColor left, LightColor right)
        {
            return !left.Equals(right);
        }
    }

    public class LightFrame
    {
        private List<LightLayer> _layers;

        private LightLayer _touchLayer;
        
        public List<LightLayer> layers => this._layers;
        
        public LightLayer this[int index] => this._layers[index];

        public LightFrame()
        {
            _layers = new List<LightLayer> { new LightLayer() };
        }
        
        public LightFrame(LightColor color) : this()
        {
            _layers[0].FillColor(color);
        }
        
        public LightFrame(LightColor[] colors) : this()
        {
            _layers[0].colors = colors;
        }

        public void AddLayer(LightLayer layer)
        {
            layers.Add(layer);
        }
        
        public void AddTouchData(List<ActiveSegment> segments)
        {
            _touchLayer = LightLayer.FromTouchData(segments);
        }
        
        internal void Flatten(ref LightColor[] buffer)
        {
            foreach (var layer in _layers)
            {
                for (var i = 0; i < 480; i++)
                {
                    LightColor light = layer[i];
                    if (light.a != 0)
                    {
                        buffer[i] = light;
                    }
                }
            }

            if (_touchLayer == null) return;
            
            for (var i = 0; i < 480; i++)
            {
                LightColor touchLight = _touchLayer[i];
                if (touchLight.a != 0)
                {
                    buffer[i] = touchLight;
                }
            }
        }
    }

    public class LightLayer
    {
        private LightColor[] _colors = new LightColor[480];
        
        /// <summary>
        /// The array of colors in this frame. <b>It must contain exactly 480 <see cref="LightColor"/> objects.</b>
        /// </summary>
        /// <exception cref="ArgumentException">The array was not the expected size of 480.</exception>
        public LightColor[] colors
        {
            get => _colors;
            set
            {
                if (value.Length != 480) throw new ArgumentException("Array of colors is not expected size");
                _colors = value;
            }
        }
        
        public LightColor this[int index]
        {
            get => _colors[index];
            set => _colors[index] = value;
        }
        
        public LightColor this[byte x, byte y]
        {
            get => GetSegmentColor(x, y);
            set => SetSegmentColor(x, y, value);
        }
        
        public void FillColor(LightColor color)
        {
            for (var i = 0; i < 480; i++) colors[i] = color;
        }
        
        /// <summary>
        /// Sets a specific segments color based on its coordinates.
        /// </summary>
        /// <param name="x">The column of the segment.</param>
        /// <param name="y">The row of the segment.</param>
        /// <param name="color1">The color for the bottom LED.</param>
        /// <param name="color2">The color for the top LED.</param>
        public void SetSegmentColor(byte x, byte y, LightColor color1, LightColor color2)
        {
            var pixels = GetPixelsInSegment(x, y);
            colors[pixels[0]] = color1;
            colors[pixels[1]] = color2;
        }
        
        /// <summary>
        /// Sets a specific segments color based on its coordinates.
        /// </summary>
        /// <param name="x">The column of the segment.</param>
        /// <param name="y">The row of the segment.</param>
        /// <param name="color">The color to set it to.</param>
        public void SetSegmentColor(byte x, byte y, LightColor color)
        {
            SetSegmentColor(x, y, color, color);
        }

        /// <summary>
        /// Sets the color of an active segment.
        /// </summary>
        /// <param name="segment">The specified segment.</param>
        /// <param name="color1">The color for the bottom LED.</param>
        /// <param name="color2">The color for the top LED.</param>
        public void SetSegmentColor(ActiveSegment segment, LightColor color1, LightColor color2)
        {
            SetSegmentColor(segment.x, segment.y, color1, color2);
        }
        
        /// <summary>
        /// Sets the color of an active segment.
        /// </summary>
        /// <param name="segment">The specified segment.</param>
        /// <param name="color">The color to set it to.</param>
        public void SetSegmentColor(ActiveSegment segment, LightColor color)
        {
            SetSegmentColor(segment, color, color);
        }
        
        public LightColor GetSegmentColor(byte x, byte y)
        {
            return _colors[GetPixelsInSegment(x, y)[0]];
        }
        
        // Fun fact, you can't fit a number 0-479 into a byte
        private static ushort[] GetPixelsInSegment(byte x, byte y)
        {
            if (x > 59 || y > 3) throw new IndexOutOfRangeException();
            // this math makes my head hurt.
            ushort lower;

            if (x < 30) // left side
                lower = (ushort)((29 - x) * 8 + (3 - y) * 2);
            else // right side
                lower = (ushort)(480 - ((x - 29) * 8 + (y - 3) * 2));
            
            return new ushort[] { lower, (ushort)(lower + 1) };
        }

        public static LightLayer FromTouchData(List<ActiveSegment> segments)
        {
            var touchLayer = new LightLayer();
            foreach (var seg in segments)
            {
                touchLayer.SetSegmentColor(seg, LightColor.White);
            }
            
            return touchLayer;
        }
    }
    
    #endregion
    
    #region Reader

    /// <summary>
    /// The exception thrown when a reader command returns a non-success state,
    /// and the method does not return a <see cref="ReaderResponseStatus"/>.
    /// </summary>
    public class ReaderException : Exception
    {
        public ReaderException() { }
        
        public ReaderException(string message) : base(message) { }
        
        public ReaderException(string message, Exception inner) : base(message, inner) { }
    }

    public struct ReaderCommand
    {
        public ReaderCommandType command;
        public byte[] payload;

        public ReaderCommand(ReaderCommandType cmd)
        {
            command = cmd;
            payload = new byte[0];
        }

        public ReaderCommand(ReaderCommandType cmd, byte[] payload)
        {
            command = cmd;
            this.payload = payload;
        }

        public static byte MakeChecksum(byte[] array)
        {
            byte chk = 0x00;
            for (var i = 0; i < array.Length - 1; i++)
            {
                unchecked { chk += array[i]; }
            }
            return chk;
        }

        public static byte[] EscapeBytes(byte[] bytes)
        {
            var result = new List<byte>();

            foreach (var b in bytes)
            {
                if (b == 0xd0 || b == 0xe0)
                {
                    result.Add(0xd0);
                    result.Add((byte)(b - 1));
                }
                else result.Add(b);
            }

            return result.ToArray();
        }

        public static byte[] UnescapeBytes(byte[] bytes)
        {
            var result = new List<byte>();
            var needsEscape = false;

            foreach (var b in bytes)
            {
                if (needsEscape)
                {
                    result.Add((byte)(b + 1));
                    needsEscape = false;
                }
                else
                {
                    if (b == 0xd0) needsEscape = true;
                    else result.Add(b);
                }
            }

            return result.ToArray();
        }
    }

    public enum ReaderCommandType
    {
        GetFirmwareVersion = 0x30,
        GetHardwareVersion = 0x32,
        RadioOn = 0x40,
        RadioOff = 0x41,
        CardPoll = 0x42,
        MifareSelectCard = 0x43,
        MifareSelectCardLong = 0x44,
        MifareSetKeyA = 0x50,
        MifareAuthKeyA = 0x51,
        MifareReadBlock = 0x52,
        MifareWriteBlock = 0x53,
        MifareSetKeyB = 0x54,
        MifareAuthKeyB = 0x55,
        Reset = 0x62,
        FeliCa1 = 0x70,
        FeliCa2 = 0x71,
        LightSetChannel = 0x80,
        LightSetColor = 0x81,
        LightGetInfo = 0xF0,
        LightGetVersion = 0xF1,
        LightReset = 0xF5,
    }
    
    public struct ReaderResponse
    {
        public ReaderCommandType command;
        public ReaderResponseStatus status;
        public byte[] payload;

        public ReaderResponse(byte[] raw)
        {
            if (raw[0] != 0xe0) throw new InvalidDataException($"Invalid response (read {raw[0]:X2}, expected E0)");
            command = (ReaderCommandType)raw[4];
            status = (ReaderResponseStatus)raw[5];
            payload = new byte[raw[6]];
            if (raw[6] != 0) Array.Copy(raw, 7, payload, 0, payload.Length);
        }

        public override string ToString()
        {
            return $"ReaderResponse: {command} ({status}) {{{BitConverter.ToString(payload)}}} [{payload.Length}]";
        }
    }

    /// <summary>
    /// Contains information about a card.
    /// </summary>
    public struct ReaderCard
    {
        public readonly ReaderCardType type;
        
        // Mifare
        public readonly byte[] uid;
        public byte[] accessCode;
        
        // FeliCa
        public readonly byte[] idm;
        public readonly byte[] pmm;
        
        /// <summary>
        /// Creates a new card with the provided information.
        /// </summary>
        /// <param name="type">The type of card to create.</param>
        /// <param name="data">The ID of the card, as a byte array.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ReaderCard(ReaderCardType type, byte[] data)
        {
            this.type = type;
            uid = idm = pmm = accessCode = new byte[0];

            switch (type)
            {
                case ReaderCardType.Mifare:
                    uid = data;
                    break;
                case ReaderCardType.FeliCa:
                    idm = new byte[8];
                    Array.Copy(data, 0, idm, 0, 8);
                    pmm = new byte[8];
                    Array.Copy(data, 7, pmm, 0, 8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// Converts the information in this card into a user-facing identifier for the card.
        /// </summary>
        /// <returns>The user-facing identifier for the card.</returns>
        /// <exception cref="InvalidDataException">Thrown if the card data is invalid in some way.</exception>
        /// <remarks>
        /// This is only intended for displaying the card information to the user.
        /// <b>Do not use this string for data processing, the representation is subject to change at any time.</b>
        /// </remarks>
        public override string ToString()
        {
            switch (type)
            {
                case ReaderCardType.Mifare:
                    if (accessCode.Length != 10) throw new InvalidDataException("Access code is not valid");
                    
                    var sb = new StringBuilder();
                    for (var i = 0; i < 10; i += 2)
                    {
                        sb.AppendFormat("{0:X2}{1:X2}", accessCode[i], accessCode[i + 1]);
                        if (i < 8) sb.Append('-');
                    }
                    
                    return sb.ToString();
                case ReaderCardType.FeliCa:
                    if (idm.Length != 8) throw new InvalidDataException("IDm is not valid");

                    string code;
                    
                    try
                    {
                        code = AmuseIC.GetID(idm);
                    }
                    catch (ArgumentException)
                    {
                        // only use "0008" code when the card is not recognized as an AIC
                        var bytes = idm;
                        if (BitConverter.IsLittleEndian) Array.Reverse(idm);
                        code = BitConverter.ToUInt64(bytes, 0).ToString().PadLeft(20, '0');
                    }
                    
                    return Regex.Replace(code, ".{4}", "$0 ");
                default:
                    throw new InvalidDataException("Invalid card type");
            }
        }
    }
    
    /// <summary>
    /// The status returned when a <see cref="ReaderResponse"/> is received.
    /// </summary>
    public enum ReaderResponseStatus
    {
        Ok = 0x00,
        CardError = 0x01,
        NotAccepted = 0x02,
        InvalidCommand = 0x03,
        InvalidData = 0x04,
        ChecksumError = 0x05,
        InternalError = 0x06
    }

    /// <summary>
    /// A card type.
    /// Also used in flag form by <see cref="ReaderController.RadioOn"/> to select which card types to accept.
    /// </summary>
    [Flags]
    public enum ReaderCardType
    {
        Mifare = 1,
        FeliCa = 2,
        Both = Mifare | FeliCa
    }

    /// <summary>
    /// A color channel for the lights in the reader.
    /// </summary>
    [Flags]
    public enum ReaderColorChannel
    {
        Red = 0x1,
        Green = 0x2,
        Blue = 0x4
    }
    
    #endregion

    #region IO4

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

    #endregion
}