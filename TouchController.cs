using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace LilyConsole
{
    public class TouchController
    {
        private TouchManager RingL;
        private TouchManager RingR;

        public TouchController(string leftPort = "COM4", string rightPort = "COM3")
        {
            RingL = new TouchManager(leftPort, 'L');
            RingR = new TouchManager(rightPort, 'R');
        }

        public void Initialize()
        {
            RingL.Initialize();
            RingR.Initialize();
        }
        
        public static bool ValidateChecksum(byte[] packet)
        {
            byte chk = 0x00;
            for (var i = 0; i < packet.Length - 1; i++)
                chk ^= packet[i];
            return packet[packet.Length] == chk;
        }
    }

    public class TouchManager
    {
        private SerialPort port;

        public bool isRight => letter == 'R';
        public string syncVersion = string.Empty;
        public string[] unitVersions = new string[6];

        private bool streamMode = false;
        private byte[] lastRawData = new byte[24];
        private bool[,] touchData = new bool[4,30];
        private List<ActiveSegment> segments = new List<ActiveSegment>();
        private byte loopState = 0;

        public readonly char letter;

        public TouchManager(string portName, char letter)
        {
            this.letter = char.ToUpper(letter);
            if (this.letter != 'R' && this.letter != 'L')
            {
                throw new Exception($"Letter {this.letter} is unknown to TouchManager.");
            }
            this.port = new SerialPort(portName, 115200);
        }

        public void Initialize()
        {
            GetSyncVersion();
            GetUnitVersion();
        }
        
        public void GetSyncVersion()
        {
            SendCommand(Command.GET_SYNC_BOARD_VER);
            syncVersion = Encoding.ASCII.GetString(ReadData(8).Data);
        }

        public void GetUnitVersion()
        {
            SendCommand(Command.GET_UNIT_BOARD_VER);
            var info = Encoding.ASCII.GetString(ReadData(45).Data);
            syncVersion = info.Substring(0, 6);
            if (info[6] != letter) throw new Exception("Sync Board disagrees which side it is!");
            for (int i = 0; i < 6; i++)
            {
                unitVersions[i] = info.Substring(7+(i*6), 6);
            }
        }

        public void StartTouchStream()
        {
            SendData(new byte[] { (byte)Command.START_AUTO_SCAN, 0x7F, 0x3F, 0x64, 0x28, 0x44, 0x3B, 0x3A });
            var ack = ReadData(3); // read ack
            if (ack.Command != (byte)Command.START_AUTO_SCAN)
                throw new Exception("Start Scan message was not acknowledged.");
            streamMode = true;
            port.DataReceived += DataReceived;
        }

        public void DebugInfo()
        {
            Console.WriteLine("TouchManager Info:");
            Console.WriteLine($"Side: {letter}");
            Console.WriteLine($"Inverted input: {isRight}");
            Console.WriteLine($"Sync Board version: {syncVersion}");
            Console.WriteLine($"Unit Board versions: {string.Join(",",unitVersions)}");
            Console.WriteLine("===");
            Console.WriteLine($"Loop state: {loopState}");
            Console.WriteLine($"Currently touched segments: {segments.Count}");
        }

        public void DebugTouch()
        {
            Console.WriteLine("Current Touch Frame:");
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 30; column++)
                {
                    Console.Write(touchData[row, column] ? "\u2588" : "\u2591");
                }
                Console.Write("\n");
            }
            Console.WriteLine($"Loop state: {loopState}");
            Console.WriteLine($"Currently touched segments: {segments.Count}");
        }

        private bool[,] GetTouchData(byte[] stream = null)
        {
            segments.Clear();
            var raw = stream != null ? new TouchCommand(stream) : ReadData(36);

            bool[,] touchData = new bool[4, 30];

            loopState = raw.Data[raw.Data.Length - 1];
            Buffer.BlockCopy(raw.Data, 0, lastRawData, 0, 24);

            for (int panel = 0; panel < 6; panel++)
            {
                for (int row = 0; row < 4; row++)
                {
                    var rowData = lastRawData[row + (panel * 6)];
                    for (int segment = 0; segment < 5; segment++)
                    {
                        var active = (rowData & (1 << segment)) != 0;
                        
                        var x = row;
                        var y = (panel * 6) + segment;
                        
                        if (active)
                        {
                            // this looks like shit
                            segments.Add(new ActiveSegment() {x = x, y = y});
                        }
                        touchData[x, y] = active;
                    }
                }
            }

            this.touchData = touchData;
            return touchData;
        } 

        private void SendByte(byte data)
        {
            SendData(new[]{data});
        }
        
        private void SendCommand(Command data)
        {
            SendData(new[]{(byte)data});
        }
        
        private void SendData(byte[] data)
        {
            port.Write(data, 0, data.Length);
        }
        
        private TouchCommand ReadData(int size)
        {
            var raw = new byte[size];
            port.Read(raw, 0, size);
            
            if (!TouchController.ValidateChecksum(raw))
            {
                throw new Exception("Checksum failure!");
            }

            return new TouchCommand(raw);
        }
        
        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Handle incoming data
            SerialPort sp = (SerialPort)sender;
            int bytesToRead = sp.BytesToRead;

            byte[] buffer = new byte[bytesToRead];
            sp.Read(buffer, 0, bytesToRead);

            // Process the received data (e.g., display or use it)
            Console.WriteLine($"Received data: {BitConverter.ToString(buffer)}");

            GetTouchData();
        }
    }

    public class TouchCommand
    {
        public byte Command;
        public byte[] Data;
        public byte Checksum;

        public TouchCommand(byte[] raw)
        {
            Data = new byte[raw.Length - 2];
            Command = raw[0];
            Checksum = raw[raw.Length - 1];
            Buffer.BlockCopy(raw, 1, Data, 0, Data.Length);
        }
    }

    public struct ActiveSegment
    {
        public int x;
        public int y;
    }

    public enum Command
    {
        NEXT_WRITE = 0x20,
        NEXT_READ = 0x72,
        BEGIN_WRITE = 0x77,
        TOUCH_DATA = 0x81,
        UNKNOWN_2 = 0x94,
        GET_SYNC_BOARD_VER = 0xA0,
        UNKNOWN_1 = 0xA2,
        UNKNOWN_READ = 0xA3,
        GET_UNIT_BOARD_VER = 0xA8,
        START_AUTO_SCAN = 0xC9,
    }
}