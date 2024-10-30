using System;
#if UNITY
using UnityEngine;
#endif

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
        #if UNITY
        public static LightLayer Gradient(Color32 outerColor, Color32 innerColor)
        #else
        public static LightLayer Gradient(LightColor outerColor, LightColor innerColor)
        #endif
        {
            var layer = new LightLayer();

            #if UNITY
            var colors = new Color32[8];
            #else
            var colors = new LightColor[8];
            #endif
            
            for (var row = 0; row < 8; row++)
            {
                var ratio = (float)row / 7;
                var r = (byte)(outerColor.r + ratio * (innerColor.r - outerColor.r));
                var g = (byte)(outerColor.g + ratio * (innerColor.g - outerColor.g));
                var b = (byte)(outerColor.b + ratio * (innerColor.b - outerColor.b));

                #if UNITY
                colors[row] = new Color32(r,g,b,0xFF);
                #else
                colors[row] = new LightColor(r,g,b);
                #endif
            }

            for (var i = 1; i < 60; i++)
            {
                Array.Copy(colors, 0, layer.colors, i*8, 8);
            }

            return layer;
        }
    }
}