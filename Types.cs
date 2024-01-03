using System;
using System.Collections.Generic;
using System.Globalization;

namespace LilyConsole
{
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
        /// <remarks>Range: 0-29</remarks>
        public byte y { get; }

        public ActiveSegment(byte x, byte y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public struct LightColor
    {
        public static readonly LightColor Red = new LightColor(255, 0, 0);
        public static readonly LightColor Green = new LightColor(0, 255, 0);
        public static readonly LightColor Blue = new LightColor(0, 0, 255);
        public static readonly LightColor White = new LightColor(255, 255, 255);
        public static readonly LightColor Off = new LightColor();
        
        public byte r { get; }
        public byte g { get; }
        public byte b { get; }
        
        public LightColor(byte r, byte g, byte b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }
    }

    public class LightFrame
    {
        private LightColor[] _colors;

        public LightColor[] colors
        {
            get => _colors;
            set
            {
                if (value.Length != 480) throw new ArgumentException("Array of colors is not expected size");
                _colors = value;
            }
        }

        public LightFrame()
        {
            colors = new LightColor[480];
        }

        public LightFrame(LightColor[] colors)
        {
            this.colors = colors;
        }

        public void FillColor(LightColor color)
        {
            for (var i = 0; i < colors.Length; i++) colors[i] = color;
        }

        public void SetSegmentColor(byte x, byte y, LightColor color)
        {
            var pixels = GetPixelsInSegment(x, y);
            colors[pixels[0]] = colors[pixels[1]] = color;
        }

        private static byte[] GetPixelsInSegment(byte x, byte y)
        {
            // this math makes my head hurt.
            byte lower;
            
            if (y < 30) 
                lower = (byte)((29 - y) * 8 + (3 - x) * 2);
            else 
                lower = (byte)(238 - (y * 8 + x * 2) + 480);
            
            return new byte[] { lower, (byte)(lower + 1) };
        }
        
        private void LayerTouchData(List<ActiveSegment> segments)
        {
            foreach (var seg in segments)
            {
                SetSegmentColor(seg.x, seg.y, LightColor.White);
            }
        }
    }
}