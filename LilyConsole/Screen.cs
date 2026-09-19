using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LilyConsole
{
    public static class Screen
    {
        public static int ScreenWidth => 1080;
        public static int ScreenHeight => 1920;

        public static int WindowSize => 1080;

        public static int WindowOffset => 362;
        
        public static Point ScreenCenterAbsolute => new Point(WindowSize / 2, (WindowSize / 2) + WindowOffset);
        public static Point ScreenCenter => new Point(WindowSize / 2, WindowSize / 2);

        public static Rectangle WindowBounds => new Rectangle(0, 0, WindowSize, WindowSize);
        public static Rectangle WindowBoundsAbsolute => new Rectangle(0, WindowOffset, WindowSize, WindowSize);
        
        public static Rectangle CircleBounds => new Rectangle(
            Point.Add(WindowBounds.Location, new Size(10, 10)),
            Size.Subtract(WindowBounds.Size, new Size(20, 20))
        );
        public static Rectangle CircleBoundsAbsolute => new Rectangle(
            Point.Add(WindowBoundsAbsolute.Location, new Size(10, 10)),
            Size.Subtract(WindowBoundsAbsolute.Size, new Size(20, 20))
        );
        
        public static byte ScreenToColumn(Point point, bool absolute = true) => ScreenToColumn(point.X, point.Y, absolute);
        
        public static byte ScreenToColumn(int x, int y, bool absolute = true)
        {
            var center = absolute ? ScreenCenterAbsolute : ScreenCenter;
            double angle = (-(Math.Atan2(y - center.Y, x - center.X) * 180.0 / Math.PI) + 270) % 360;
            
            return (byte)(angle / 6);
        }

        public static Point ColumnToScreen(byte column, int depth = 0, bool absolute = true)
        {
            var center = absolute ? ScreenCenterAbsolute : ScreenCenter;
            var radius = (CircleBounds.Width / 2) - depth;
            var angle = (column * 6) + 3;

            var angleRad = -(angle - 270) * Math.PI / 180F;
            
            float x = (float)(radius * Math.Cos(angleRad)) + center.X;
            float y = (float)(radius * Math.Sin(angleRad)) + center.Y;

            return new Point((int)x, (int)y);
        }
    }

    public class DebugDraw
    {
        private IntPtr _hdc;
        private IntPtr _hwnd;
        private Graphics _graphics;
        private Pen _pen;
        private Brush _brush;

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint
        {
            public int X;
            public int Y;
            
            public CursorPoint(int x, int y)
            {
                X = x;
                Y = y;
            }
            
            public static implicit operator Point(CursorPoint point) => new Point(point.X, point.Y);
        }
        
        public bool Absolute { get; }

        [DllImport("User32")]
        private static extern IntPtr GetDC(IntPtr hwnd);
        
        [DllImport("User32")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out CursorPoint lpPoint);
        // DON'T use System.Drawing.Point, the order of the fields in System.Drawing.Point isn't guaranteed to stay the same.
        
        public DebugDraw(IntPtr? hwnd = null)
        {
            _hwnd = hwnd ?? IntPtr.Zero;
            Absolute = _hwnd == IntPtr.Zero;
            _hdc = GetDC(_hwnd);
            _graphics = Graphics.FromHdc(_hdc);

            _pen = Pens.Red;
            _brush = Brushes.Red;
        }

        public void DrawPoint(Point point, int pt = 20)
        {
            var size = new Size(pt, pt);
            _graphics.FillRectangle(_brush, new  Rectangle(Point.Subtract(point, new Size(size.Width / 2, size.Height / 2)), size));
        }
        
        public void CenterPoint()
        {
            var point = Absolute ? Screen.ScreenCenterAbsolute : Screen.ScreenCenter;
            DrawPoint(point, 20);
        }

        public void CircleBounds()
        {
            _graphics.DrawEllipse(_pen, Absolute ? Screen.CircleBoundsAbsolute : Screen.CircleBounds);
        }

        public byte DrawClosestPoint()
        {
            GetCursorPos(out var cursor);
            
            var center = Absolute ? Screen.ScreenCenterAbsolute : Screen.ScreenCenter;
            
            double vX = cursor.X - center.X;
            double vY = cursor.Y - center.Y;
            double magV = Math.Sqrt(vX*vX + vY*vY);
            double aX = center.X + vX / magV * (Screen.CircleBounds.Width / 2.0f);
            double aY = center.Y + vY / magV * (Screen.CircleBounds.Width / 2.0f);

            var p = new Point((int)aX, (int)aY);
            
            //DrawPoint(p);
            
            var col = Screen.ScreenToColumn(p, Absolute);
            
            DrawPoint(Screen.ColumnToScreen(col, depth: Screen.CircleBounds.Width, Absolute));
            
            return col;
        }

        ~DebugDraw()
        {
            if(ReleaseDC(_hwnd, _hdc) == 0) throw new Exception("Failed to release device context for debug drawing!");
        }
    }
}