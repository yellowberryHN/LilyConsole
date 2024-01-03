using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LilyConsole.Unity
{
    public static class TouchControllerExtensions
    {
        public static Vector2Int ToVector2Int(this ActiveSegment segment)
        {
            return new Vector2Int(segment.x, segment.y);
        }
        /// <summary>
        /// Gets all active segments from the controller, converted into <see cref="Vector2Int"/>.
        /// </summary>
        /// <returns>A list of active segments as <see cref="Vector2Int"/></returns>
        /// <remarks>This probably performs terribly, only use if you have to.</remarks>
        public static List<Vector2Int> GetSegmentsUnity(this TouchController controller)
        {
            return controller.segments.Select(seg => seg.ToVector2Int()).ToList();
        }
    }

    public static class TouchManagerExtensions
    {
        /// <summary>
        /// Gets all active segments from the touch manager, converted into <see cref="Vector2Int"/>.
        /// </summary>
        /// <returns>A list of active segments as <see cref="Vector2Int"/></returns>
        /// <remarks>This probably performs terribly, only use if you have to.</remarks>
        public static List<Vector2Int> GetSegmentsUnity(this TouchManager manager)
        {
            return manager.segments.Select(seg => seg.ToVector2Int()).ToList();
        }
    }

    public static class LightControllerExtensions
    {
        public static Color32 ToColor32(this LightColor lc)
        {
            return new Color32(lc.r, lc.g, lc.b, 0xFF);
        }

        public static LightColor ToLightColor(this Color32 c32)
        {
            return new LightColor(c32.r, c32.g, c32.b);
        }

        public static Color32[] GetColorsUnity(this LightFrame lf)
        {
            return lf.colors.Select(color => color.ToColor32()).ToArray();
        }

        public static void FillColor(this LightFrame lf, Color32 color)
        {
            lf.FillColor(color.ToLightColor());
        }

        public static void SetSegmentColor(this LightFrame lf, byte x, byte y, Color32 color)
        {
            lf.SetSegmentColor(x, y, color.ToLightColor());
        }
    }
}