using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LilyConsole.Helpers;
#if UNITY
using UnityEngine;
#elif GODOT
using Godot;
#endif

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
    
    /// send the unknown ones at your own peril.
    public enum TouchCommandType
    {
        NEXT_WRITE = 0x20,
        UNKNOWN_6F = 0x6F,
        UNKNOWN_71 = 0x71,
        NEXT_READ = 0x72,
        BEGIN_WRITE = 0x77,
        TOUCH_DATA = 0x81,
        UNKNOWN_91 = 0x91,
        UNKNOWN_93 = 0x93,
        SET_THRESHOLDS = 0x94,
        GET_SYNC_BOARD_VER = 0xA0,
        UNKNOWN_A2 = 0xA2,
        UNKNOWN_READ = 0xA3,
        GET_UNIT_BOARD_VER = 0xA8,
        UNKNOWN_A9 = 0xA9,
        UNKNOWN_BC = 0xBC,
        UNKNOWN_C0 = 0xC0,
        UNKNOWN_C1 = 0xC1,
        START_AUTO_SCAN = 0xC9,
    }
    
    #endregion

    #region Lights

    #if UNITY
    [Obsolete("Use Unity native Color32 instead.", true)]
    #endif
    public struct LightColor : IEquatable<LightColor>
    {
        public static LightColor Red => new LightColor(255, 0, 0);
        public static LightColor Green => new LightColor(0, 255, 0);
        public static LightColor Blue => new LightColor(0, 0, 255);
        public static LightColor White => new LightColor(255, 255, 255);
        public static LightColor Black => new LightColor(0, 0, 0);
        public static LightColor Off => new LightColor();

        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public LightColor(byte r, byte g, byte b, byte a = 0xFF)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = r.GetHashCode();
                hashCode = (hashCode * 397) ^ g.GetHashCode();
                hashCode = (hashCode * 397) ^ b.GetHashCode();
                hashCode = (hashCode * 397) ^ a.GetHashCode();
                return hashCode;
            }
        }

        public bool Equals(LightColor other)
        {
            return r == other.r && g == other.g && b == other.b && a == other.a;
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
        
        #if GODOT
        public static implicit operator LightColor(Color color)
        {
            return new LightColor((byte)color.R8, (byte)color.G8, (byte)color.B8, (byte)color.A8);
        }

        public static implicit operator Color(LightColor color)
        {
            return Color.Color8(color.r, color.g, color.b, color.a);
        }
        #endif
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
        
        #if UNITY
        public LightFrame(Color32 color) : this()
        #else
        public LightFrame(LightColor color) : this()
        #endif
        {
            _layers[0].FillColor(color);
        }

        #if UNITY
        public LightFrame(Color32[] colors) : this()
        #else
        public LightFrame(LightColor[] colors) : this()
        #endif
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

        #if UNITY
        private Color32[] Flatten()
        {
            var flat = new Color32[480];
        #else
        private LightColor[] Flatten()
        {
            var flat = new LightColor[480];
        #endif
            foreach (var layer in _layers)
            {
                for (var i = 0; i < 480; i++)
                {
                    #if UNITY
                    if (layer[i] != Color.clear)
                    #else
                    if (layer[i] != LightColor.Off)
                    #endif
                    {
                        flat[i] = layer[i];
                    }
                }
            }

            if (_touchLayer == null) return flat;
            
            for (var i = 0; i < 480; i++)
            {
                #if UNITY
                if (_touchLayer[i] != Color.clear)
                #else
                if (_touchLayer[i] != LightColor.Off)
                #endif
                {
                    flat[i] = _touchLayer[i];
                }
            }

            return flat;
        }
        
        public static explicit operator LedData(LightFrame frame)
        {
            return new LedData { unitCount = 480, rgbaValues = frame.Flatten() };
        }
    }

    public class LightLayer
    {
        #if UNITY
        private Color32[] _colors = new Color32[480];
        #else
        private LightColor[] _colors = new LightColor[480];
        #endif

        #if UNITY
        /// <summary>
        /// The array of colors in this frame. <b>It must contain exactly 480 <see cref="UnityEngine.Color32"/> objects.</b>
        /// </summary>
        /// <exception cref="ArgumentException">The array was not the expected size of 480.</exception>
        public Color32[] colors
        #else
        /// <summary>
        /// The array of colors in this frame. <b>It must contain exactly 480 <see cref="LightColor"/> objects.</b>
        /// </summary>
        /// <exception cref="ArgumentException">The array was not the expected size of 480.</exception>
        public LightColor[] colors
        #endif
        {
            get => _colors;
            set
            {
                if (value.Length != 480) throw new ArgumentException("Array of colors is not expected size");
                _colors = value;
            }
        }
        
        #if UNITY
        public Color32 this[int index]
        #else
        public LightColor this[int index]
        #endif
        {
            get => _colors[index];
            set => _colors[index] = value;
        }
        
        #if UNITY
        public Color32 this[byte x, byte y]
        #else
        public LightColor this[byte x, byte y]
        #endif
        {
            get => GetSegmentColor(x, y);
            set => SetSegmentColor(x, y, value);
        }

        /// <summary>
        /// Fills every pixel with the same color.
        /// </summary>
        /// <param name="color">The color to use.</param>
        #if UNITY
        public void FillColor(Color32 color)
        #else
        public void FillColor(LightColor color)
        #endif
        {
            for (var i = 0; i < 480; i++) colors[i] = color;
        }
        
        /// <summary>
        /// Sets a specific segments color based on its coordinates.
        /// </summary>
        /// <param name="x">The row of the segment.</param>
        /// <param name="y">The column of the segment.</param>
        /// <param name="color">The color to set it to.</param>
        #if UNITY
        public void SetSegmentColor(byte x, byte y, Color32 color)
        #else
        public void SetSegmentColor(byte x, byte y, LightColor color)
        #endif
        {
            var pixels = GetPixelsInSegment(x, y);
            colors[pixels[0]] = colors[pixels[1]] = color;
        }

        /// <summary>
        /// Sets the color of an active segment.
        /// </summary>
        /// <param name="segment">The specified segment.</param>
        /// <param name="color">The color to set it to.</param>
        #if UNITY
        public void SetSegmentColor(ActiveSegment segment, Color32 color)
        #else
        public void SetSegmentColor(ActiveSegment segment, LightColor color)
        #endif
        {
            SetSegmentColor(segment.x, segment.y, color);
        }

        #if UNITY
        public Color32 GetSegmentColor(byte x, byte y)
        #else
        public LightColor GetSegmentColor(byte x, byte y)
        #endif
        {
            return _colors[GetPixelsInSegment(x, y)[0]];
        }
        
        // Fun fact, you can't fit a number 0-479 into a byte
        private static ushort[] GetPixelsInSegment(byte x, byte y)
        {
            if (x > 3 || y > 59) throw new IndexOutOfRangeException();
            // this math makes my head hurt.
            ushort lower;

            if (y < 30) // left side
                lower = (ushort)((29 - y) * 8 + (3 - x) * 2);
            else // right side
                lower = (ushort)(480 - ((y - 29) * 8 + (x - 3) * 2));
            
            return new ushort[] { lower, (ushort)(lower + 1) };
        }

        public static LightLayer FromTouchData(List<ActiveSegment> segments)
        {
            var touchLayer = new LightLayer();
            foreach (var seg in segments)
            {
                #if UNITY
                touchLayer.SetSegmentColor(seg, Color.white);
                #else
                touchLayer.SetSegmentColor(seg, LightColor.White);
                #endif
            }
            
            return touchLayer;
        }
    }
    
    #endregion
    
    #region Reader
    
    public struct ReaderCommand
    {
        public ReaderCommandType command;
        public byte[] payload;

        public ReaderCommand(ReaderCommandType cmd)
        {
            this.command = cmd;
            this.payload = new byte[0];
        }

        public ReaderCommand(ReaderCommandType cmd, byte[] payload)
        {
            this.command = cmd;
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
            if (raw[0] != 0xe0) throw new Exception($"Invalid response (read {raw[0]:X2}, expected E0)");
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

    public struct ReaderCard
    {
        public ReaderCardType type;
        
        // Mifare
        public readonly byte[] uid;
        public byte[] accessCode;
        
        // FeliCa
        public readonly byte[] idm;
        public readonly byte[] pmm;
        
        public ReaderCard(ReaderCardType type, byte[] data)
        {
            this.type = type;
            this.uid = this.idm = this.pmm = this.accessCode = new byte[0];

            switch (type)
            {
                case ReaderCardType.Mifare:
                    this.uid = data;
                    break;
                case ReaderCardType.FeliCa:
                    Array.Copy(data, 0, this.idm, 0, 8);
                    Array.Copy(data, 8, this.pmm, 0, 8);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

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
                    
                    var bytes = idm;
                    if (BitConverter.IsLittleEndian) Array.Reverse(idm);
                    
                    // perpetuating the heinous misnomer of an "0008" code
                    return BitConverter.ToUInt64(bytes, 0).ToString().PadLeft(20, '0');
                default:
                    throw new ArgumentException("Invalid card type");
            }
        }
    }
    
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

    [Flags]
    public enum ReaderCardType
    {
        Mifare = 1,
        FeliCa = 2
    }

    [Flags]
    public enum ReaderColorChannel
    {
        Red = 0x1,
        Green = 0x2,
        Blue = 0x4
    }
    
    #endregion
}