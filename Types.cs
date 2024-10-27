using System;
using System.Collections.Generic;
using LilyConsole.Helpers;

namespace LilyConsole
{
    // TOUCH
    
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

    // LIGHTS
    
    public struct LightColor
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
    }

    public class LightFrame
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
        
        public LightFrame() {}
        
        public LightFrame(LightColor color)
        {
            FillColor(color);
        }

        public LightFrame(LightColor[] colors)
        {
            this.colors = colors;
        }
        
        /// <summary>
        /// Fills every pixel with the same color.
        /// </summary>
        /// <param name="color">The color to use.</param>
        public void FillColor(LightColor color)
        {
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
        }

        /// <summary>
        /// Sets a specific segments color based on its coordinates.
        /// </summary>
        /// <param name="x">The row of the segment.</param>
        /// <param name="y">The column of the segment.</param>
        /// <param name="color">The color to set it to.</param>
        /// <seealso cref="SetSegmentColor(LilyConsole.ActiveSegment, LilyConsole.LightColor)"/>
        public void SetSegmentColor(byte x, byte y, LightColor color)
        {
            var pixels = GetPixelsInSegment(x, y);
            colors[pixels[0]] = colors[pixels[1]] = color;
        }

        /// <summary>
        /// Sets the color of an active segment.
        /// </summary>
        /// <param name="segment">The specified segment.</param>
        /// <param name="color">The color to set it to.</param>
        public void SetSegmentColor(ActiveSegment segment, LightColor color)
        {
            SetSegmentColor(segment.x, segment.y, color);
        }
        
        // Fun fact, you can't fit a number 0-479 into a byte
        private static ushort[] GetPixelsInSegment(byte x, byte y)
        {
            // this math makes my head hurt.
            ushort lower;

            if (y < 30) // left side
                lower = (ushort)((29 - y) * 8 + (3 - x) * 2);
            else // right side
                lower = (ushort)(480 - ((y - 29) * 8 + (x - 3) * 2));
            
            return new ushort[] { lower, (ushort)(lower + 1) };
        }
        
        public void AddTouchData(List<ActiveSegment> segments)
        {
            foreach (var seg in segments)
            {
                SetSegmentColor(seg.x, seg.y, LightColor.White);
            }
        }
        
        public static explicit operator LedData(LightFrame frame)
        {
            return new LedData { unitCount = (uint)frame.colors.Length, rgbaValues = frame.colors };
        }
    }
}