using LilyConsole;
using System;
using System.Net.NetworkInformation;

namespace LilyConsoleTesting
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Pick an option:");
                Console.WriteLine("1) combined touch");
                Console.WriteLine("2) touch left");
                Console.WriteLine("3) touch right");
                Console.WriteLine("4) vfd");
                Console.WriteLine("5) lights");
                Console.WriteLine("6) card reader");
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
                }
            }
        }

        public static void TouchLTest()
        {
            var RingL = new TouchManager("COM4", 'L');

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
            var RingR = new TouchManager("COM3", 'R');

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
            lights.Initialize();
        }
    }
}