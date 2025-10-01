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
                Console.WriteLine("8) card reader lights");
                Console.WriteLine("9) io4");
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
                    case '9':
                        IO4Test();
                        break;
                }
            }
        }

        // this is not a proper way to do things, don't do this.
        public static void CleanUp()
        {
            USBIntLED.Safe_USBIntLED_set(0, LedData.blank);
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
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                if (RingL.segments.Count > 0) RingL.DebugTouch();
            }
            RingL.Close();
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
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                if (RingR.segments.Count > 0) RingR.DebugTouch();
            }
            RingR.Close();
        }

        public static void TouchCombinedTest()
        {
            var controller = new TouchController();
            controller.Initialize();
            Console.CursorVisible = false;
            Console.WriteLine("Starting touch streams!");
            controller.StartTouchStream();
            Console.WriteLine("Started!");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                controller.GetTouchData();
                controller.DebugTouch();
            }
            controller.Close();
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
            }

            var frame = new LightFrame(new LightColor(255, 0, 255));
            
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                controller.GetTouchData();
                frame.AddTouchData(controller.segments);
                lights.SendLightFrame(frame);
                //lights.SendLightFrame(frame, controller.segments);
                controller.DebugTouch();
            }
            
            controller.Close();
            lights.Close();
        }

        public static void VFDTest()
        {
            var vfd = new VFDController();
            vfd.Initialize();
            vfd.PowerOn();
            vfd.Write("Hello!");
            Console.Write("Input: ");
            var text = Console.ReadLine();
            vfd.Clear();
            vfd.Write(text.Length > 20 ? text.Substring(0, 20) : text);
            Console.ReadKey();
            vfd.Close();
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
            
            Console.ReadKey();

            var gradientFrame = new LightFrame
            {
                layers =
                {
                    [0] = LightPatternGenerator.Gradient(LightColor.White, LightColor.Blue)
                }
            };

            lights.SendLightFrame(gradientFrame);
            
            Console.ReadKey();
            
            float time = 0;
            const float cyclePeriod = 2 * (float)Math.PI;
            
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                var r = (byte)(127.5 * (Math.Sin(time) + 1));
                var g = (byte)(127.5 * (Math.Sin(time + 2 * Math.PI / 3) + 1));
                var b = (byte)(127.5 * (Math.Sin(time + 4 * Math.PI / 3) + 1));
                Thread.Sleep(15);
                lights.SendLightFrame(new LightFrame(new LightColor(r, g, b)));
                
                time += 0.0005f; 
                if (time >= cyclePeriod) time -= cyclePeriod;
            }
            
            lights.Close();
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
            ReaderResponseStatus pollStatus;
            while (reader.lastPoll.Count == 0 || Console.KeyAvailable)
            {
                Thread.Sleep(150); // minimum recommended delay is 150ms, or about ever 10 frames at 60hz
                Console.Write('.');
                if ((pollStatus = reader.Poll()) != ReaderResponseStatus.Ok)
                {
                    Console.WriteLine($"Poll returned {pollStatus}!");
                }
            }
            Console.WriteLine(Environment.NewLine);
            try
            {
                var cardInfo = reader.ReadCardInfo();
                Console.WriteLine($"Access Code: {cardInfo}");
                reader.SetColor(LightColor.Green);
            }
            catch (ReaderException e)
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
            
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                var r = (byte)(127.5 * (Math.Sin(time) + 1));
                var g = (byte)(127.5 * (Math.Sin(time + 2 * Math.PI / 3) + 1));
                var b = (byte)(127.5 * (Math.Sin(time + 4 * Math.PI / 3) + 1));
                reader.SetColor(r, g, b);
                
                time += 0.0005f; 
                if (time >= cyclePeriod) time -= cyclePeriod;
            }

            reader.Close();
        }
        
        // this does an extremely poor job at demonstrating what I was trying to, thread.sleep is slow as fuck
        private static void IO4Test()
        {
            var board = new IO4Controller();
            board.Initialize();
            Console.WriteLine("Board started!");
            Console.ReadKey(true);
            board.SetColor(LightColor.White);
            Console.WriteLine("Lights enabled!");
            Console.ReadKey(true);
            board.SetColor(new LightColor(255, 0, 255));
            board.ClearBuffer();
            Console.WriteLine("Going full ham! (no delay)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                board.Poll();
                board.ButtonLights();
            }
            board.SetColor(LightColor.Green);
            Console.WriteLine("Simulating ideal input poll rate (8ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                board.Poll();
                board.ButtonLights();
                Thread.Sleep(8);
            }
            board.SetColor(new LightColor(255, 255, 0));
            Console.WriteLine("Simulating typical 60hz game loop (17ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                board.Poll(1);
                board.ButtonLights();
                Thread.Sleep(17);
            }
            board.SetColor(LightColor.Red);
            Console.WriteLine("Simulating 30hz game loop (33ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                board.Poll(3);
                board.ButtonLights();
                Thread.Sleep(33);
            }
            board.Close();
        }
    }
}