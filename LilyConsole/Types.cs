using System;
using System.Collections.Generic;
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
}