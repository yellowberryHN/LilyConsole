using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace LilyConsole
{
    public class TouchController
    {
        /// <summary>
        /// Manager for the left side of the console.
        /// </summary>
        private TouchManager RingL;
        /// <summary>
        /// Manager for the right side of the console.
        /// </summary>
        private TouchManager RingR;

        /// <summary>
        /// The last retrieved touch information as a multi-dimensional array (4x60).
        /// </summary>
        public bool[,] touchData = new bool[4, 60];
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public List<ActiveSegment> segments = new List<ActiveSegment>();

        /// <summary>
        /// Creates a new touch controller interface. This does not attempt communications with the console until
        /// <see cref="Initialize"/> is called.
        /// </summary>
        /// <param name="leftPort">The name passed to <see cref="SerialPort"/> for the left side of the console.</param>
        /// <param name="rightPort">The name passed to <see cref="SerialPort"/> for the right side of the console.</param>
        public TouchController(string leftPort = "COM4", string rightPort = "COM3")
        {
            RingL = new TouchManager(leftPort, 'L');
            RingR = new TouchManager(rightPort, 'R');
        }

        /// <summary>
        /// Creates a new connection to both sides of the console.
        /// </summary>
        public void Initialize()
        {
            RingL.Initialize();
            RingR.Initialize();
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// </summary>
        public void Close()
        {
            RingL.Close();
            RingR.Close();
        }

        /// <summary>
        /// Instructs all panels to start transmitting touch data.
        /// </summary>
        public void StartTouchStream()
        {
            RingL.StartTouchStream();
            RingR.StartTouchStream();
        }

        /// <summary>
        /// Retrieves the latest touch data from both sides of the console and combine them.
        /// The data is also used to update <see cref="touchData"/> every time this is called.
        /// </summary>
        /// <returns>The latest touch data in a multi-dimensional array (4x60).</returns>
        public bool[,] GetTouchData()
        {
            segments.Clear();

            var touchL = RingL.touchData;
            var touchR = RingR.touchData;

            for (byte row = 0; row < 4; row++)
            {
                for (byte column = 0; column < 30; column++)
                {
                    if(this.touchData[row, column] = touchL[row, column])
                    {
                        segments.Add(new ActiveSegment(row, column));
                    }
                }
                
                for (byte column = 0; column < 30; column++)
                {
                    // mirror the right side to normalize the data.
                    if(this.touchData[row, column + 30] = touchR[row, 29 - column])
                    {
                        segments.Add(new ActiveSegment(row, (byte)(column + 30)));
                    }
                }
            }

            return this.touchData;
        }

        /// <summary>
        /// Debugging method, intended to be called from a loop to get realtime touch information as it changes.
        /// Outputs a graphic to the console of the current touch state.
        /// </summary>
        /// <remarks>
        /// It is recommended that you disable <see cref="Console.CursorVisible"/> to prevent flickering.
        /// </remarks>
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

        /// <summary>
        /// Validates the checksum on the end of a given full payload.
        /// </summary>
        /// <param name="packet">The bytes of the payload to be validated.</param>
        /// <returns>The validity of the checksum</returns>
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

        /// <summary>
        /// Used to determine if normalized data must be mirrored.
        /// </summary>
        public bool isRight => letter == 'R';
        /// <summary>
        /// The version string of the Sync Board.
        /// </summary>
        public string syncVersion = string.Empty;
        /// <summary>
        /// The version strings of all 6 Unit Boards, present in each of the 6 panels.
        /// Probably not a good sign if these don't match.
        /// </summary>
        public string[] unitVersions = new string[6];
        
        private bool streamMode = false;
        private byte[] lastRawData = new byte[24];
        
        /// <summary>
        /// The last retrieved touch information as a multi-dimensional array (4x30).
        /// The coordinates are relative to the inner top corner of their side being 0,0
        /// </summary>
        public bool[,] touchData = new bool[4,30];
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public List<ActiveSegment> segments = new List<ActiveSegment>();
        public byte loopState = 0;

        /// <summary>
        /// The letter identifier of the side of the console this is.
        /// </summary>
        public readonly char letter;
        
        /// <param name="portName">The name passed to <see cref="SerialPort"/> for the specified side of the console.</param>
        /// <param name="letter">The letter code of the side. Must be 'L' or 'R'.</param>
        /// <exception cref="Exception">An exception will be thrown if the letter code is not 'L' or 'R'.</exception>
        public TouchManager(string portName, char letter)
        {
            this.letter = char.ToUpper(letter);
            if (this.letter != 'R' && this.letter != 'L')
            {
                throw new Exception($"Letter {this.letter} is unknown to TouchManager.");
            }
            this.port = new SerialPort(portName, 115200);
        }

        /// <summary>
        /// Creates a new connection to this side of the console.
        /// </summary>
        /// <remarks>This does not do anything if the connection is already open, to prevent weird states.</remarks> 
        public void Initialize()
        {
            if (port.IsOpen) return;
            port.Open();
            ShutUpPlease();
            GetSyncVersion();
            GetUnitVersion();
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// Flushes out any data that remains.
        /// </summary>
        /// <remarks>This does not do anything if the connection is not open, to prevent weird states.</remarks>
        public void Close()
        {
            if (!port.IsOpen) return;
            // TODO: make this tell the panels to shut the fuck up.
            port.DataReceived -= DataReceived;
            streamMode = false;
            touchData = new bool[4, 30];
            segments.Clear();
            loopState = 0;
            syncVersion = String.Empty;
            unitVersions = new string[6];
            port.Close();
        }

        /// <summary>
        /// <b>THIS IS A HACK.</b> We can get the sync board to stop streaming data by asking it for the sync board
        /// version repeatedly. This is certainly not a good solution, but it works.
        /// </summary>
        private void ShutUpPlease()
        {
            SendCommand(Command.GET_SYNC_BOARD_VER);
            SendCommand(Command.GET_SYNC_BOARD_VER);
            SendCommand(Command.GET_SYNC_BOARD_VER);
            port.DiscardInBuffer();
        }
        
        /// <summary>
        /// Asks the Sync Board to return it's version string.
        /// As a consequence of sending this command, all touch data communications are halted if they are being sent.
        /// </summary>
        private void GetSyncVersion()
        {
            port.DiscardInBuffer();
            SendCommand(Command.GET_SYNC_BOARD_VER);
            syncVersion = Encoding.ASCII.GetString(ReadData(8).Data);
        }

        /// <summary>
        /// Asks the Sync Board to provide all of the information about the Unit Boards as well as which side it is.
        /// </summary>
        /// <exception cref="Exception">
        /// Due to the assumptions we make depending on which side we are talking to, if the Sync Board reports
        /// that it isn't the side we think it is, this method will throw an exception.
        /// </exception>
        private void GetUnitVersion()
        {
            SendCommand(Command.GET_UNIT_BOARD_VER);
            var info = Encoding.ASCII.GetString(ReadData(45).Data);
            syncVersion = info.Substring(0, 6);
            if (info[6] != letter) throw new Exception("Sync Board disagrees which side it is!");
            for (var i = 0; i < 6; i++)
            {
                unitVersions[i] = info.Substring(7+(i*6), 6);
            }
        }

        /// <summary>
        /// Instructs the panels to start streaming touch data over the connection.
        /// </summary>
        /// <exception cref="Exception">The <see cref="Command.START_AUTO_SCAN"/> message was not acknowledged,
        /// something went wrong.</exception>
        public void StartTouchStream()
        {
            // magic bytes, what do they do?????? who knows.
            SendData(new byte[] { (byte)Command.START_AUTO_SCAN, 0x7F, 0x3F, 0x64, 0x28, 0x44, 0x3B, 0x3A });
            var ack = ReadData(3); // read ack
            if (ack.Command != (byte)Command.START_AUTO_SCAN)
                throw new Exception("Start Scan message was not acknowledged.");
            streamMode = true;
            port.DataReceived += DataReceived;
        }
        
        /// <summary>
        /// Debugging method, retrieves some information about the connected side of the console.
        /// </summary>
        public void DebugInfo()
        {
            Console.WriteLine("TouchManager Info:");
            Console.WriteLine($"Side: {letter}");
            Console.WriteLine($"Mirrored input: {isRight}");
            Console.WriteLine($"Sync Board version: {syncVersion}");
            Console.WriteLine($"Unit Board versions: {string.Join(",",unitVersions)}");
            Console.WriteLine("===");
            Console.WriteLine($"Loop state: {loopState}");
            Console.WriteLine($"Currently touched segments: {segments.Count}");
        }

        /// <summary>
        /// Debugging method, intended to be called from a loop to get realtime touch information as it changes.
        /// Outputs a graphic to the console of the current touch state.
        /// </summary>
        /// <remarks>
        /// It is recommended that you disable <see cref="Console.CursorVisible"/> to prevent flickering.
        /// </remarks>
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

        /// <summary>
        /// Retrieves the latest touch data from the panels.
        /// The data is also used to update <see cref="touchData"/> every time this is called.
        /// </summary>
        /// <returns>The latest touch data in a multi-dimensional array (4x30).</returns>
        /// <exception cref="Exception">
        /// If the provided command or read data is not touch data an exception will be thrown.
        /// </exception>
        private bool[,] GetTouchData(TouchCommand stream = null)
        {
            segments.Clear();
            var raw = stream ?? ReadData(36);
            if (raw.Command != (byte)Command.TOUCH_DATA) throw new Exception("that's not touch data.");

            this.touchData = new bool[4, 30];

            loopState = raw.Data[raw.Data.Length - 1];
            Buffer.BlockCopy(raw.Data, 0, lastRawData, 0, 24);
            
            for (byte row = 0; row < 4; row++)
            {
                for (byte panel = 0; panel < 6; panel++)
                {
                    var rowData = lastRawData[panel + (row * 6)];
                    for (byte segment = 0; segment < 5; segment++)
                    {
                        var active = (rowData & (1 << segment)) != 0;
                        
                        var x = (byte)row;
                        var y = (byte)(segment + (panel * 5));
                        
                        if (active) segments.Add(new ActiveSegment(x, y));
                        this.touchData[x, y] = active;
                    }
                }
            }

            return this.touchData;
        } 
        /// <summary>
        /// Sends a command to the Sync Board.
        /// </summary>
        /// <param name="data">The <see cref="Command"/> to send.</param>
        private void SendCommand(Command data)
        {
            SendData(new[]{(byte)data});
        }
        
        /// <summary>
        /// Sends arbitrary data to the Sync Board.
        /// </summary>
        /// <param name="data">The byte array to send.</param>
        private void SendData(byte[] data)
        {
            port.Write(data, 0, data.Length);
        }
        
        /// <summary>
        /// Reads returned data from the Sync Board.
        /// </summary>
        /// <param name="size">How many bytes to read.</param>
        /// <returns>The returned data, as a <see cref="TouchCommand"/>.</returns>
        /// <exception cref="Exception">
        /// If the returned data does not have a valid checksum, an exception will be thrown.
        /// </exception>
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

        /// <summary>
        /// Is called every time data is received after touch streaming is enabled.
        /// If the data to read exceeds 36 characters (the size of a touch "frame"),
        /// it will process the data. Not terribly elegant, but it works.
        /// </summary>
        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // this is probably really stupid
            if (port.BytesToRead >= 36)
            {
                GetTouchData();
            }
        }
    }

    /// <summary>
    /// A wrapper for a touch board command.
    /// </summary>
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

    /// send the unknown ones at your own peril.
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