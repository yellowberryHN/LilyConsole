using LilyConsole;
using System;
using System.Drawing;
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
                    case 'd':
                        DebugDrawTest();
                        break;
                }
            }
        }

        private static void DebugDrawTest()
        {
            var dd = new DebugDraw();
            Console.WriteLine("Testing drawing!");
            
            var lights = new LightController();
            if (!lights.Initialize())
            {
                Console.WriteLine("Failed to load lights!");
            };

            var testFrame = new LightFrame(LightColor.Blue);
            testFrame.AddLayer(new LightLayer());
            
            lights.SendLightFrame(testFrame);

            bool circle = true;
            bool point = true;
            bool mouse = true;
            
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var conKey = Console.ReadKey(true);

                    switch (conKey.Key)
                    {
                        case ConsoleKey.P:
                            point = !point;
                            break;
                        case ConsoleKey.C:
                            circle = !circle;
                            break;
                        case ConsoleKey.M:
                            mouse = !mouse;
                            break;
                    }
                    
                    if (conKey.Key == ConsoleKey.Escape) break;
                }
                
                if (point) dd.CenterPoint();
                if (circle) dd.CircleBounds();
                if (mouse)
                {
                    var column = dd.DrawClosestPoint();
                    var layer = new LightLayer();
                    layer[column, 0] = layer[column, 1] = layer[column, 2] = layer[column, 3] =LightColor.Red;
                    testFrame.layers[1] = layer;
                    Thread.Sleep(16);
                    lights.SendLightFrame(testFrame);
                }
            }
            
            lights.Close();
        }

        // this is not a proper way to do things, don't do this.
        public static void CleanUp()
        {
            USBIntLED.Safe_USBIntLED_set(0, LedData.blank);
            USBIntLED.Safe_USBIntLED_Terminate();
        }

        public static void TouchLTest()
        {
            var ringL = new SyncBoardController("COM4", SyncBoardSide.Left);

            ringL.Initialize();
            ringL.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            ringL.StartPolling();
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                if (ringL.Segments.Count > 0) ringL.DebugTouch();
            }
            ringL.Close();
        }

        public static void TouchRTest()
        {
            var ringR = new SyncBoardController("COM3", SyncBoardSide.Right);

            ringR.Initialize();
            ringR.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            ringR.StartPolling();
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                if (ringR.Segments.Count > 0) ringR.DebugTouch();
            }
            ringR.Close();
        }

        public static void TouchCombinedTest()
        {
            var controller = new TouchController();
            controller.Initialize();
            Console.CursorVisible = false;
            Console.WriteLine("Starting touch streams!");
            controller.StartPolling();
            Console.WriteLine("Started!");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
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
            controller.StartPolling();
            Console.WriteLine("Started!");
            
            var lights = new LightController();
            if (!lights.Initialize())
            {
                Console.WriteLine("Failed to load lights!");
            }

            var frame = new LightFrame(new LightColor(255, 0, 255));
            bool debugShown = true;
            
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var conKey = Console.ReadKey(true);

                    if (conKey.Key == ConsoleKey.Escape) break;
                    
                    switch (conKey.Key)
                    {
                        case ConsoleKey.D:
                            debugShown = !debugShown;
                            break;
                    }
                }
                
                frame.AddTouchData(controller.Segments);
                lights.SendLightFrame(frame);
                
                //lights.SendLightFrame(frame, controller.segments);
                if(debugShown) controller.DebugTouch();
                // sleep for as short as possible, 1ms is not attainable
                Thread.Sleep(1);
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
            var text = Console.ReadLine() ?? "你好bro";
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
            int timeout = 25;
            
            while (true)
            {
                var r = (byte)(127.5 * (Math.Sin(time) + 1));
                var g = (byte)(127.5 * (Math.Sin(time + 2 * Math.PI / 3) + 1));
                var b = (byte)(127.5 * (Math.Sin(time + 4 * Math.PI / 3) + 1));
                Thread.Sleep(timeout);
                lights.SendLightFrame(new LightFrame(new LightColor(r, g, b)));
                
                time += 0.05f;
                if (time >= cyclePeriod) time -= cyclePeriod;

                if (Console.KeyAvailable)
                {
                    var conKey = Console.ReadKey(true);
                    switch (conKey.Key)
                    {
                        case ConsoleKey.UpArrow:
                            timeout += 5;
                            Console.WriteLine($"[+] timeout = {timeout}");
                            break;
                        case ConsoleKey.DownArrow:
                            timeout -= 5;
                            Console.WriteLine($"[-] timeout = {timeout}");
                            break;
                    }

                    if (conKey.Key == ConsoleKey.Escape) break;
                }
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
            while (reader.lastPoll.Count == 0)
            {
                // minimum recommended delay is 150ms, or about every 10 frames at 60hz
                Thread.Sleep(150);
                
                Console.Write('.');
                if ((pollStatus = reader.Poll()) != ReaderResponseStatus.Ok)
                {
                    Console.WriteLine($"Poll returned {pollStatus}!");
                }

                if (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Escape) continue;
                reader.Close();
                return;
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
            var io4 = new IO4Controller();
            io4.Initialize();
            Console.WriteLine("Board started!");
            Console.ReadKey(true);
            io4.SetColor(LightColor.White);
            Console.WriteLine("Lights enabled!");
            Console.ReadKey(true);
            io4.SetColor(new LightColor(255, 0, 255));
            Console.WriteLine("Going full ham! (no delay)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                io4.ButtonLights();
            }
            io4.SetColor(LightColor.Green);
            Console.WriteLine("Simulating ideal input poll rate (8ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                io4.ButtonLights();
                Thread.Sleep(8);
            }
            io4.SetColor(new LightColor(255, 255, 0));
            Console.WriteLine("Simulating typical 60hz game loop (17ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                io4.ButtonLights();
                Thread.Sleep(17);
            }
            io4.SetColor(LightColor.Red);
            Console.WriteLine("Simulating 30hz game loop (33ms)");
            while (!(Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
            {
                io4.ButtonLights();
                Thread.Sleep(33);
            }
            io4.Close();
        }
    }
}