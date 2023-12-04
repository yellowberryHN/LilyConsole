using LilyConsole;

namespace LilyConsoleTesting
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            //var controller = new TouchController();
            //controller.Initialize();
            
            var RingL = new TouchManager("COM4", 'L');
            var RingR = new TouchManager("COM3", 'R');
            
            RingL.Initialize();
            RingL.DebugInfo();
        }
    }
}