using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;

namespace LilyConsole
{
    public class VFDController
    {
        SerialPort port;
        public Lang language { get; private set; } = Lang.SIMP_CHINESE;
        public Font font { get; private set; } = Font._16_16;
        public Bright brightness { get; private set; } = Bright._100;
        public bool powered { get; private set; } = false;

        /// <summary>
        /// Establish a connection to a VFD, and prepare it for use.
        /// </summary>
        /// <param name="portName">The name passed to <see cref="SerialPort"/> for the VFD.</param>
        /// <exception cref="System.IO.IOException">Will be thrown if serial port was not found.</exception>
        public VFDController(string portName = "COM2")
        {
            port = new SerialPort(portName, 115200);
        }

        /// <summary>
        /// Initializes the display and sets some sane default settings.
        /// </summary>
        /// <remarks>
        /// Note that you may see the display flash for a moment when running this.
        /// This is due to the fact that the display must be powered on to change settings.
        /// </remarks>
        public void Initialize()
        {
            port.Open();
            Reset();
            PowerOn();
            Brightness(Bright._50);
            CanvasShift(0);
            Cursor(0, 0);
            Language(Lang.JAPANESE);
            FontSize(Font._16_16);
            PowerOff(); // this might reset all of these settings??? idk.
        }
        
        private void RawWrite(byte number)
        {
            Console.WriteLine(BitConverter.ToString(new byte[] { number }));
            port.Write(new byte[] { number }, 0, 1);
        }

        private void RawWrite(byte[] bytes)
        {
            Console.WriteLine(BitConverter.ToString(bytes));
            port.Write(bytes, 0, bytes.Length);
        }
        
        private void RawWrite(short x)
        {
            byte hi = (byte)((x & 0x100) >> 8);
            byte lo = (byte)(x & 0xFF);
            RawWrite(new[] {hi, lo});
        }

        private void RawWrite(string text)
        {
            Encoding unicodeEncoding = Encoding.Unicode;
            Encoding correctEncoding = Encoding.GetEncoding(_langMap[language]);
            
            byte[] unicodeBytes = unicodeEncoding.GetBytes(text);
            byte[] encodedBytes = Encoding.Convert(unicodeEncoding, correctEncoding, unicodeBytes);

            Console.WriteLine(BitConverter.ToString(encodedBytes));
            port.Write(encodedBytes, 0, encodedBytes.Length);
        }

        /// <summary>
        /// Write text to the VFD at the current cursor position.
        /// </summary>
        /// <param name="text">The text to write.</param>
        public void Write(string text)
        {
            RawWrite(text);
        }

        /// <summary>
        /// Resets the VFD to stock settings. You probably don't need to call this.
        /// </summary>
        public void Reset()
        {
            RawWrite(new byte[] { 0x1B, 0x0B });
        }

        /// <summary>
        /// Clears the VFD of any text or bitmaps.
        /// </summary>
        public void Clear()
        {
            RawWrite(new byte[] { 0x1B, 0x0C });
        }

        /// <summary>
        /// The allowed brightness values in %.
        /// </summary>
        public enum Bright {
            _0 = 0,
            _25 = 1,
            _50 = 2,
            _75 = 3,
            _100 = 4
        }

        /// <summary>
        /// Sets the brightness of the VFD.
        /// </summary>
        /// <param name="brightness">The brightness level.</param>
        public void Brightness(Bright brightness)
        {
            RawWrite(new byte[] { 0x1B, 0x20, (byte)brightness });
        }

        /// <summary>
        /// Turns on the VFD.
        /// </summary>
        public void PowerOn()
        {
            Power(true);
        }

        /// <summary>
        /// Turns off the VFD.
        /// </summary>
        public void PowerOff()
        {
            Power(false);
        }

        /// <summary>
        /// Changes the power state of the VFD. Use <see cref="PowerOn"/> and <see cref="PowerOff"/> for convenience.
        /// </summary>
        /// <param name="power">The power state desired.</param>
        public void Power(bool power)
        {
            RawWrite(new byte[] { 0x1B, 0x21, (byte)(power ? 0x01 : 0x00) });
        }

        /// <summary>
        /// Moves the entire canvas over by <paramref name="left"/> pixels.
        /// </summary>
        /// <param name="left">The amount to move left.</param>
        public void CanvasShift(short left)
        {
            RawWrite(new byte[] { 0x1B, 0x22 });
            RawWrite(left);
        }

        /// <summary>
        /// Changes the position of the cursor.
        /// </summary>
        /// <param name="left">Pixels from the left.</param>
        /// <param name="top">Pixels from the top.</param>
        public void Cursor(short left, byte top)
        {
            RawWrite(new byte[] { 0x1B, 0x30 });
            RawWrite(left);
            RawWrite(top);
        }

        /// <summary>
        /// The supported languages of the VFD.
        /// </summary>
        public enum Lang {
            SIMP_CHINESE,
            TRAD_CHINESE,
            JAPANESE,
            KOREAN
        }

        private readonly Dictionary<Lang, int> _langMap = new Dictionary<Lang, int>()
        {
            { Lang.SIMP_CHINESE, 936 },
            { Lang.TRAD_CHINESE, 950 },
            { Lang.JAPANESE, 932 },
            { Lang.KOREAN, 949 }
        };

        /// <summary>
        /// Sets the current language of the VFD. Text written with another language active will still remain.
        /// </summary>
        /// <param name="lang">The language to choose.</param>
        public void Language(Lang lang)
        {
            language = lang;
            RawWrite(new byte[] { 0x1B, 0x32, (byte)language });
        }

        /// <summary>
        /// The supported font sizes of the VFD.
        /// </summary>
        public enum Font
        {
            _16_16,
            _6_8
        }

        /// <summary>
        /// Sets the font size of the VFD. Text written in another font size will still remain.
        /// </summary>
        /// <param name="size">The size to choose.</param>
        public void FontSize(Font size)
        {
            // 3
            font = size;
            RawWrite(new byte[] { 0x1B, 0x33, (byte)size });
        }

        /// <summary>
        /// Creates a scroll box within the area selected.
        /// </summary>
        public void CreateScrollBox(short left, byte top, short width, byte height)
        {
            RawWrite(new byte[] { 0x1B, 0x40 });
            RawWrite(left);
            RawWrite(top);
            RawWrite(width);
            RawWrite(height);
        }
        
        /// <summary>
        /// The speed of the scrolling. The smaller the number, the faster.
        /// </summary>
        /// <param name="divisor">The desired scroll speed.</param>
        public void ScrollSpeed(byte divisor)
        {
            RawWrite(new byte[] { 0x1B, 0x41, (byte)divisor });
        }

        /// <summary>
        /// The text to scroll within the scroll box.
        /// </summary>
        /// <param name="text">The text to scroll.</param>
        /// <exception cref="ArgumentOutOfRangeException">Text over 255 characters long is unsupported.</exception>
        /// <remarks>The scroll box must be first defined with <see cref="CreateScrollBox"/>.</remarks>
        public void ScrollText(string text)
        {
            if (text.Length >= 0x100) throw new ArgumentOutOfRangeException("Text is too long.");
            RawWrite(new byte[] { 0x1B, 0x50, (byte)text.Length });
            RawWrite(text);
        }

        /// <summary>
        /// Start scrolling the text within the scroll box.
        /// </summary>
        /// <remarks>The scroll box must be first defined with <see cref="CreateScrollBox"/>.</remarks>
        public void ScrollStart()
        {
            RawWrite(new byte[] { 0x1B, 0x51 });
        }

        /// <summary>
        /// Stop scrolling the text within the scroll box.
        /// </summary>
        /// <remarks>The scroll box must be first defined with <see cref="CreateScrollBox"/>.</remarks>
        public void ScrollStop()
        {
            RawWrite(new byte[] { 0x1B, 0x52 });
        }

        /// <summary>
        /// The type of blink to use for <see cref="BlinkSet"/>.
        /// </summary>
        public enum BlinkMode
        {
            Off = 0,
            Invert = 1,
            All = 2
        }

        /// <summary>
        /// Sets the blink type and interval for the screen.
        /// </summary>
        /// <param name="blink">The type of blink to use.</param>
        /// <param name="interval">How quickly you want it to blink.</param>
        public void BlinkSet(BlinkMode blink, byte interval)
        {
            RawWrite(new byte[] { 0x1B, 0x23, (byte)blink, interval });
        }

        /// <summary>
        /// Clears a specific line.
        /// </summary>
        /// <param name="line">The line to clear.</param>
        public void ClearLine(byte line)
        {
            Cursor(0, line);
            RawWrite("".PadLeft(20));
            Cursor(0, line);
        }

        public void DrawBitmap(Bitmap bmp, Point origin)
        {
            if (bmp == null || bmp.PixelFormat != PixelFormat.Format1bppIndexed)
                throw new ArgumentException("Provided bitmap is not monochrome");

            // We have to do it this way because of a GDI+ bug.
            bmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
            RotateNoneFlipYMono(bmp);

            var bounds = new Rectangle(new Point(), bmp.Size);

            var data = bmp.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format1bppIndexed);

            RawWrite(new byte[] { 0x1B, 0x2E });
            RawWrite((short)origin.X);
            RawWrite((byte)origin.Y);
            RawWrite((short)bmp.Height); // Inverted because image was flipped
            RawWrite((byte)((bmp.Width / 8)-1));

            int bytes = ( bmp.Width * bmp.Height ) / 8;

            // Create a byte array to hold the pixel data
            byte[] pixelData = new byte[bytes];

            // Copy the data from the pointer to the byte array
            Marshal.Copy(data.Scan0, pixelData, 0, bytes);

            RawWrite(pixelData);

            bmp.UnlockBits(data);
        }

        /// <summary>
        /// Cleans up after using the VFD.
        /// </summary>
        public void CleanUp()
        {
            Clear();
            Reset();
            PowerOff();
        }

        private static void RotateNoneFlipYMono(Bitmap bmp)
        {
            if (bmp == null || bmp.PixelFormat != PixelFormat.Format1bppIndexed)
                throw new ArgumentException("Provided bitmap is not monochrome");

            var height = bmp.Height;
            var width = bmp.Width;
            // width in dwords
            var stride = (width + 31) >> 5;
            // total image size
            var size = stride * height;
            // alloc storage for pixels
            var bytes = new int[size];

            // get image pixels
            var rect = new Rectangle(Point.Empty, bmp.Size);
            var bd = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
            Marshal.Copy(bd.Scan0, bytes, 0, size);

            // flip by swapping dwords
            int halfSize = size >> 1;
            for (int y1 = 0, y2 = size - stride; y1 < halfSize; y1 += stride, y2 -= stride)
            {
                int end = y1 + stride;
                for (int x1 = y1, x2 = y2; x1 < end; x1++, x2++)
                {
                    bytes[x1] ^= bytes[x2];
                    bytes[x2] ^= bytes[x1];
                    bytes[x1] ^= bytes[x2];
                }
            }

            // copy pixels back
            Marshal.Copy(bytes, 0, bd.Scan0, size);
            bmp.UnlockBits(bd);
        }
        
        /// <summary>
        /// Cleans up when the VFD controller is garbage collected.
        /// </summary>
        ~VFDController()
        {
            CleanUp();
        }
    }
}
