using LilyConsole;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LilyConsole.Helpers;

namespace LilyConsoleTesting
{
    internal class Program
    {
        private delegate bool ConsoleCtrlHandlerDelegate(int sig);

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate handler, bool add);

        static ConsoleCtrlHandlerDelegate _consoleCtrlHandler;
        
        public static void Main(string[] args)
        {
            _consoleCtrlHandler += s =>
            {
                CleanUp();
                return false;   
            };
            SetConsoleCtrlHandler(_consoleCtrlHandler, true);
            
            while (true)
            {
                Console.WriteLine("Pick an option:");
                Console.WriteLine("1) combined touch");
                Console.WriteLine("2) touch left");
                Console.WriteLine("3) touch right");
                Console.WriteLine("4) vfd");
                Console.WriteLine("5) lights");
                Console.WriteLine("6) card reader");
                Console.WriteLine("7) combined touch with lights");
                Console.WriteLine("8) card reader color test");
                var choice = Console.ReadKey(true);

                switch (choice.KeyChar)
                {
                    case '1':
                        TouchCombinedTest();
                        break;
                    case '2':
                        TouchLTest();
                        break;
                    case '3':
                        TouchRTest();
                        break;
                    case '4':
                        VFDTest();
                        break;
                    case '5':
                        LightTest();
                        break;
                    case '6':
                        ReaderTest();
                        break;
                    case '7':
                        TouchCombinedTestWithLights();
                        break;
                    case '8':
                        ReaderColorTest();
                        break;
                }
            }
        }

        // this is not a proper way to do things, don't do this.
        public static void CleanUp()
        {
            USBIntLED.Safe_USBIntLED_set(0, (LedData)new LightFrame());
            USBIntLED.Safe_USBIntLED_Terminate();
        }

        public static void TouchLTest()
        {
            var RingL = new SyncBoardController("COM4", 'L');

            RingL.Initialize();
            RingL.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            RingL.StartTouchStream();
            while (true)
            {
                if (RingL.segments.Count > 0) RingL.DebugTouch();
            }
        }

        public static void TouchRTest()
        {
            var RingR = new SyncBoardController("COM3", 'R');

            RingR.Initialize();
            RingR.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            RingR.StartTouchStream();
            while (true)
            {
                if (RingR.segments.Count > 0) RingR.DebugTouch();
            }
        }

        public static void TouchCombinedTest()
        {
            var controller = new TouchController();
            controller.Initialize();
            Console.CursorVisible = false;
            Console.WriteLine("Starting touch streams!");
            controller.StartTouchStream();
            Console.WriteLine("Started!");
            while (true)
            {
                controller.GetTouchData();
                controller.DebugTouch();
            }
        }
        
        public static void TouchCombinedTestWithLights()
        {
            var controller = new TouchController();
            controller.Initialize();
            Console.CursorVisible = false;
            Console.WriteLine("Starting touch streams!");
            controller.StartTouchStream();
            Console.WriteLine("Started!");
            
            var lights = new LightController();
            if (!lights.Initialize())
            {
                Console.WriteLine("Failed to load lights!");
            };
            
            while (true)
            {
                controller.GetTouchData();
                lights.SendLightFrame(new LightFrame(new LightColor(255, 0, 255)), controller.segments);
                controller.DebugTouch();
            }
        }

        public static void VFDTest()
        {
            var vfd = new VFDController();
            vfd.Initialize();
            vfd.PowerOn();
            vfd.Write("Hello!");
        }

        public static void LightTest()
        {
            var lights = new LightController();
            if (!lights.Initialize())
            {
                Console.WriteLine("Failed to load lights!");
            };

            var testFrame = new LightFrame(LightColor.Green);

            lights.SendLightFrame(testFrame);
            
            Console.ReadKey();
            
            lights.SendLightFrame(new LightFrame(LightColor.Red));

            /*
            Console.ReadKey();

            var gradientFrame = new LightFrame
            {
                layers =
                {
                    [0] = LightPatternGenerator.Gradient(LightColor.Blue, LightColor.Red)
                }
            };

            lights.SendLightFrame(gradientFrame);
            */

            Console.ReadKey();
            
            lights.SendLightFrame(new LightFrame());
        }

        public static void ReaderTest()
        {
            var reader = new ReaderController();
            reader.Initialize();
            Console.WriteLine("Reader started!");
            reader.SetColor(LightColor.White);
            reader.DebugMode = Console.ReadKey(true).Key == ConsoleKey.D;
            reader.SetColor(LightColor.Blue);
            reader.RadioOn(); // very important step
            Console.WriteLine("Polling!");
            while (reader.lastPoll.Count == 0 || Console.KeyAvailable)
            {
                Thread.Sleep(500); // minimum recommended delay is 150ms
                Console.Write('.');
                reader.Poll();
            }
            Console.WriteLine(Environment.NewLine);
            try
            {
                var cardInfo = reader.ReadCardInfo();
                Console.WriteLine($"Access Code: {cardInfo}");
                reader.SetColor(LightColor.Green);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Couldn't read card: {e.Message}");
                reader.SetColor(LightColor.Red);
            }
            Thread.Sleep(2000);
            reader.Close();
        }

        public static void ReaderColorTest()
        {
            var reader = new ReaderController();
            reader.Initialize();
            Console.WriteLine("Reader started!");
            Console.WriteLine("Debug?");
            reader.DebugMode = Console.ReadKey(true).Key == ConsoleKey.D;
            
            float time = 0;
            const float cyclePeriod = 2 * (float)Math.PI;
            
            while (true)
            {
                var r = (byte)(127.5 * (Math.Sin(time) + 1));
                var g = (byte)(127.5 * (Math.Sin(time + 2 * Math.PI / 3) + 1));
                var b = (byte)(127.5 * (Math.Sin(time + 4 * Math.PI / 3) + 1));
                reader.SetColor(r, g, b);
                
                time += 0.0005f; 
                if (time >= cyclePeriod) time -= cyclePeriod;
            }
        }
    }
}