using LilyConsole;
using System;
using System.Net.NetworkInformation;

namespace LilyConsoleTesting
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var lights = new LightController();
            lights.Initialize();

            /*
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
            */

            //var RingL = new TouchManager("COM3", 'L');
            //var RingR = new TouchManager("COM3", 'R');

            /*
            RingL.Initialize();
            RingL.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            RingL.StartTouchStream();
            while(true)
            {
                if(RingL.segments.Count > 0) RingL.DebugTouch();
            }
            */

            /*
            RingR.Initialize();
            RingR.DebugInfo();
            Console.ReadKey();
            Console.WriteLine("Starting Touch Stream...");
            Console.CursorVisible = false;
            RingR.StartTouchStream();
            while (true)
            {
                if (RingR.segments.Count > 0) RingR.DebugTouch();
            }*/
        }
    }
}