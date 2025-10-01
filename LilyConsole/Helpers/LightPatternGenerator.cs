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
                var r = (byte)(outerColor.r + ratio * (innerColor.r - outerColor.r));
                var g = (byte)(outerColor.g + ratio * (innerColor.g - outerColor.g));
                var b = (byte)(outerColor.b + ratio * (innerColor.b - outerColor.b));

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