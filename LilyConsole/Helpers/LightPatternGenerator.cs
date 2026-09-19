using System;

namespace LilyConsole.Helpers
{
    public static class LightPatternGenerator
    {
        /// <summary>
        /// Creates a circular gradient from the 
        /// </summary>
        /// <param name="outerColor">The color to use for the outer part of the ring</param>
        /// <param name="innerColor">The color to use for the inner part of the ring (closest to the screen)</param>
        /// <returns></returns>
        public static LightLayer Gradient(LightColor outerColor, LightColor innerColor)
        {
            var layer = new LightLayer();
            var colors = new LightColor[8];
            
            for (var column = 0; column < 8; column++)
            {
                var ratio = (float)column / 7;
                var r = (byte)(outerColor.R + ratio * (innerColor.R - outerColor.R));
                var g = (byte)(outerColor.G + ratio * (innerColor.G - outerColor.G));
                var b = (byte)(outerColor.B + ratio * (innerColor.B - outerColor.B));

                colors[column] = new LightColor(r, g, b);
            }

            for (var i = 0; i < 60; i++)
            {
                Array.Copy(colors, 0, layer.colors, i*8, 8);
            }

            return layer;
        }
    }
}