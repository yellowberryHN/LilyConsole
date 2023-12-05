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

        public bool[,] touchData = new bool[4, 60];
        public List<ActiveSegment> segments = new List<ActiveSegment>();

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

        public void StartTouchStream()
        {
            RingL.StartTouchStream();
            RingR.StartTouchStream();
        }

        public bool[,] GetTouchData()
        {
            segments.Clear();

            var touchL = RingL.touchData;
            var touchR = RingR.touchData;

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 30; column++)
                {
                    if(this.touchData[row, column] = touchL[row, column])
                    {
                        segments.Add(new ActiveSegment(row, column));
                    }
                }

                for (int column = 0; column < 30; column++)
                {
                    if(this.touchData[row, column + 30] = touchR[row, 29 - column])
                    {
                        segments.Add(new ActiveSegment(row, column + 30));
                    }
                }
            }

            return this.touchData;
        }

        public void DebugTouch()
        {
            Console.WriteLine("Current Touch Frame:");
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 60; column++)
                {
                    Console.Write(touchData[row, column] ? "\u2588" : "\u2591");
                }
                Console.Write("\n");
            }
            Console.WriteLine($"Loop state: L: {RingL.loopState,3}, R: {RingR.loopState,3}");
            Console.WriteLine($"Currently touched segments: {segments.Count,3}");
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 7);
        }

        public static bool ValidateChecksum(byte[] packet)
        {
            byte chk = 0x00;
            for (var i = 0; i < packet.Length - 1; i++)
                chk ^= packet[i];
            chk ^= 128;
            return packet[packet.Length-1] == chk;
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
        public bool[,] touchData = new bool[4,30];
        public List<ActiveSegment> segments = new List<ActiveSegment>();
        public byte loopState = 0;

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
            port.Open();
            GetSyncVersion();
            GetUnitVersion();
        }

        public void Close()
        {
            // TODO: make this tell the panels to shut the fuck up.
            port.Close();
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
            Console.WriteLine($"Loop state: {loopState,3}");
            Console.WriteLine($"Currently touched segments: {segments.Count}");
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop-7);
        }

        private bool[,] GetTouchData(TouchCommand stream = null)
        {
            segments.Clear();
            var raw = stream != null ? stream : ReadData(36);
            if (raw.Command != (byte)Command.TOUCH_DATA) throw new Exception("that's not touch data.");

            this.touchData = new bool[4, 30];

            loopState = raw.Data[raw.Data.Length - 1];
            Buffer.BlockCopy(raw.Data, 0, lastRawData, 0, 24);
            
            for (int row = 0; row < 4; row++)
            {
                for (int panel = 0; panel < 6; panel++)
                {
                    var rowData = lastRawData[panel + (row * 6)];
                    for (int segment = 0; segment < 5; segment++)
                    {
                        var active = (rowData & (1 << segment)) != 0;
                        
                        var x = row;
                        var y = segment + (panel * 5);

                        //if(isRight) y = 29 - y;
                        
                        if (active)
                        {
                            // this looks like shit
                            segments.Add(new ActiveSegment(x, y));
                        }
                        this.touchData[x, y] = active;
                    }
                }
            }

            return this.touchData;
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
            while (port.BytesToRead < size) {
              
            }
            port.Read(raw, 0, size);
            
            if (!TouchController.ValidateChecksum(raw))
            {
                throw new Exception("Checksum failure!");
            }

            return new TouchCommand(raw);
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // this is probably really stupid
            if (port.BytesToRead >= 36)
            {
                GetTouchData();
            }
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

        public ActiveSegment(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public enum Command
    {
        NEXT_WRITE = 0x20,
        UNKNOWN_6 = 0x6F,
        UNKNOWN_7 = 0x71,
        NEXT_READ = 0x72,
        BEGIN_WRITE = 0x77,
        TOUCH_DATA = 0x81,
        UNKNOWN_4 = 0x91,
        UNKNOWN_5 = 0x93,
        UNKNOWN_2 = 0x94,
        GET_SYNC_BOARD_VER = 0xA0,
        UNKNOWN_1 = 0xA2,
        UNKNOWN_READ = 0xA3,
        GET_UNIT_BOARD_VER = 0xA8,
        UNKNOWN_3 = 0xA9,
        UNKNOWN_10 = 0xBC,
        UNKNOWN_9 = 0xC0,
        UNKNOWN_8 = 0xC1,
        START_AUTO_SCAN = 0xC9,
    }
}