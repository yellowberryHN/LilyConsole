using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace LilyConsole
{
    public class TouchController
    {
        /// <summary>
        /// Manager for the left side of the console.
        /// </summary>
        private SyncBoardController ringL;
        /// <summary>
        /// Manager for the right side of the console.
        /// </summary>
        private SyncBoardController ringR;

        private string leftPort;
        private string rightPort;

        /// <summary>
        /// When to draw lights, flips state after every read to even out to ~62fps
        /// </summary>
        /// <remarks>
        /// Only checks the state of the left sync board, but if you don't have half the controller,
        /// you have bigger issues than lights.
        /// </remarks>
        public bool ShouldDrawLights => ringL.ShouldDrawLights; 

        /// <summary>
        /// The last retrieved touch information as a multidimensional array (4x60).
        /// <br/><br/>
        /// IMPORTANT: <see cref="TouchController.TouchData"/> is accessed [Y,X], <see cref="ActiveSegment"/> is addressed (X,Y)! 
        /// </summary>
        /// <remarks>Potentially subject to race conditions, depending on how you set up your touch polling.</remarks>
        public bool[,] TouchData = new bool[4, 60];
        
        /// <summary>
        /// Sets if the touchData buffer should be cleared before writing to it again.
        /// Mostly relevant in cases of race conditions.
        /// <list type="bullet">
        /// <item><b>Enabled</b> — The buffer will be cleared, potential for dropped inputs</item>
        /// <item><b>Disabled</b> — The buffer will not be cleared, potential for ghost inputs</item>
        /// </list>
        /// </summary>
        public bool ClearBuffer {
            get => ringL.ClearBuffer;
            set {
                ringL.ClearBuffer = value;
                ringR.ClearBuffer = value;
            }
        }
        
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public List<ActiveSegment> Segments = new List<ActiveSegment>();

        /// <summary>
        /// Creates a new touch controller interface. This does not attempt communications with the console until
        /// <see cref="Initialize"/> is called.
        /// </summary>
        /// <param name="leftPort">The name passed to <see cref="SerialPort"/> for the left side of the console.</param>
        /// <param name="rightPort">The name passed to <see cref="SerialPort"/> for the right side of the console.</param>
        public TouchController(string leftPort = "COM4", string rightPort = "COM3")
        {
            this.leftPort = leftPort;
            this.rightPort = rightPort;
        }

        /// <summary>
        /// Creates a new connection to both sides of the console.
        /// </summary>
        /// <exception cref="System.IO.IOException">Will be thrown if serial port was not found.</exception>
        public void Initialize()
        {
            ringL = new SyncBoardController(leftPort, 'L');
            ringR = new SyncBoardController(rightPort, 'R');
            ringL.Initialize();
            ringR.Initialize();
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// </summary>
        public void Close()
        {
            ringL.Close();
            ringR.Close();
        }

        /// <summary>
        /// Instructs all panels to start transmitting touch data.
        /// </summary>
        public void StartTouchStream()
        {
            ringL.StartTouchStream();
            ringR.StartTouchStream();
        }

        /// <summary>
        /// Retrieves the latest touch data from both sides of the console and combine them.
        /// The data is also used to update <see cref="TouchData"/> every time this is called.
        /// </summary>
        /// <returns>The latest touch data in a multi-dimensional array (4x60).</returns>
        public bool[,] GetTouchData()
        {
            Segments.Clear();

            var touchL = ringL.TouchData;
            var touchR = ringR.TouchData;

            for (byte row = 0; row < 4; row++)
            {
                for (byte column = 0; column < 30; column++)
                {
                    if(TouchData[row, column] = touchL[row, column])
                    {
                        Segments.Add(new ActiveSegment(column, row));
                    }
                }
                
                for (byte column = 0; column < 30; column++)
                {
                    // mirror the right side to normalize the data.
                    if(TouchData[row, column + 30] = touchR[row, 29 - column])
                    {
                        Segments.Add(new ActiveSegment((byte)(column + 30), row));
                    }
                }
            }

            return TouchData;
        }
        
        /// <summary>
        /// A <see cref="StringBuilder"/> used by <see cref="DebugTouch"/> to increase console performance.
        /// </summary>
        private readonly StringBuilder _debugSb = new StringBuilder();

        /// <summary>
        /// Debugging method, intended to be called from a loop to get realtime touch information as it changes.
        /// Outputs a graphic to the console of the current touch state.
        /// </summary>
        /// <remarks>
        /// It is recommended that you disable <see cref="Console.CursorVisible"/> to prevent flickering.
        /// </remarks>
        public void DebugTouch()
        {
            _debugSb.Clear();
            for (byte row = 0; row < 4; row++)
            {
                for (byte column = 0; column < 60; column++)
                {
                    _debugSb.Append(TouchData[row, column] ? "\u2588" : "\u2591");
                }
                _debugSb.Append("\n");
            }
            Console.WriteLine("Current Touch Frame:");
            Console.Write(_debugSb.ToString());
            Console.WriteLine($"Loop state: L: {ringL.LoopState,3}, R: {ringR.LoopState,3}");
            Console.WriteLine($"Currently touched segments: {Segments.Count,3}");
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 7);
        }
    }

    public class SyncBoardController
    {
        private SerialPort port;
        private string portName;
        
        public bool ShouldDrawLights => LoopState % 2 == 0;

        /// <summary>
        /// Used to determine if normalized data must be mirrored.
        /// </summary>
        public bool IsRight => Letter == 'R';
        /// <summary>
        /// The version string of the Sync Board.
        /// </summary>
        public string SyncVersion = string.Empty;
        /// <summary>
        /// The version strings of all 6 Unit Boards, present in each of the 6 panels.
        /// Probably not a good sign if these don't match.
        /// </summary>
        public string[] UnitVersions = new string[6];
        
        private bool streamMode = false;
        private byte[] lastRawData = new byte[24];
        
        /// <summary>
        /// The last retrieved touch information as a multidimensional array (4x30).
        /// The coordinates are relative to the inner top corner of their side being 0,0
        /// <br/><br/>
        /// IMPORTANT: <see cref="SyncBoardController.TouchData"/> is accessed [Y,X], <see cref="ActiveSegment"/> is addressed (X,Y)! 
        /// </summary>
        /// <remarks>Potentially subject to race conditions, depending on how you set up your touch polling.</remarks>
        public bool[,] TouchData = new bool[4,30];

        /// <summary>
        /// Sets if the touchData buffer should be cleared before writing to it again.
        /// Mostly relevant in cases of race conditions.
        /// <list type="bullet">
        /// <item><b>Enabled</b> — The buffer will be cleared, potential for dropped inputs</item>
        /// <item><b>Disabled</b> — The buffer will not be cleared, potential for ghost inputs</item>
        /// </list>
        /// </summary>
        public bool ClearBuffer = false;

        /// <summary>
        /// If the raw sensor reading increases above this number, the segment will be turned ON.
        /// </summary>
        public byte OnThreshold => _onThreshold;
        private byte _onThreshold = 17;
        
        /// <summary>
        /// If the raw sensor reading decreases below this number, the segment will be turned OFF.
        /// </summary>
        public byte OffThreshold => _offThreshold;
        private byte _offThreshold = 12;
        
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public List<ActiveSegment> Segments = new List<ActiveSegment>();
        public byte LoopState = 0;

        /// <summary>
        /// The letter identifier of the side of the console this is.
        /// </summary>
        public readonly char Letter;
        
        /// <param name="portName">The name passed to <see cref="SerialPort"/> for the specified side of the console.</param>
        /// <param name="letter">The letter code of the side. Must be 'L' or 'R'.</param>
        /// <exception cref="ArgumentException">Will be thrown if the letter code is not 'L' or 'R'.</exception>
        public SyncBoardController(string portName, char letter)
        {
            letter = char.ToUpper(letter);
            if (letter != 'R' && letter != 'L')
            {
                throw new ArgumentException($"Letter {letter} is unknown to TouchManager.");
            }
            Letter = letter;
            
            this.portName = portName;
        }

        /// <summary>
        /// Creates a new connection to this side of the console.
        /// </summary>
        /// <exception cref="System.IO.IOException">Will be thrown if serial port was not found.</exception>
        public void Initialize()
        {
            port = new SerialPort(portName, 115200);
            port.ReadTimeout = 0;
            
            port.Open();
            ShutUpPlease();
            GetSyncVersion();
            GetUnitVersion();
            GetActiveUnitBoards();
            SetThresholds(_onThreshold, _onThreshold);
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// Flushes out any data that remains.
        /// </summary>
        /// <remarks>This does not do anything if the connection is not open, to prevent weird states.</remarks>
        public void Close()
        {
            if (!port.IsOpen) return;
            port.DataReceived -= DataReceived;
            ShutUpPlease();
            TouchData = new bool[4, 30];
            Segments.Clear();
            LoopState = 0;
            SyncVersion = String.Empty;
            UnitVersions = new string[6];
            port.Close();
        }

        /// <summary>
        /// <b>THIS IS A HACK.</b> We can get the sync board to stop streaming data by asking it for the sync board
        /// version a bunch of times and then waiting a bit. This is certainly not a good solution, but it works.
        /// </summary>
        private void ShutUpPlease()
        {
            port.DiscardInBuffer();
            for (var i = 0; i < 5; i++)
            {
                SendCommand(TouchCommandType.GetSyncBoardVersion);
                port.DiscardInBuffer();
            }
            Thread.Sleep(20);
            port.DiscardInBuffer();
            streamMode = false;
        }
        
        /// <summary>
        /// Asks the Sync Board to return its version string.
        /// As a consequence of sending this command, all touch data communications are halted if they are being sent.
        /// </summary>
        private void GetSyncVersion()
        {
            SendCommand(TouchCommandType.GetSyncBoardVersion);
            SyncVersion = Encoding.ASCII.GetString(ReadData(8).Data);
        }

        /// <summary>
        /// Asks the Sync Board to provide all the information about the Unit Boards as well as which side it is.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// Due to the assumptions we make depending on which side we are talking to, if the Sync Board reports
        /// that it isn't the side we think it is, this method will throw an exception.
        /// </exception>
        private void GetUnitVersion()
        {
            SendCommand(TouchCommandType.GetUnitBoardVersion);
            var info = Encoding.ASCII.GetString(ReadData(45).Data);
            SyncVersion = info.Substring(0, 6);
            if (info[6] != Letter) throw new InvalidDataException("Sync Board disagrees which side it is!");
            for (var i = 0; i < 6; i++)
            {
                UnitVersions[i] = info.Substring(7+(i*6), 6);
            }
        }
        /// <summary>
        /// Asks the Sync Board which Unit Boards are currently active.
        /// </summary>
        /// <returns>A <see cref="BitArray"/> with the states of the Unit Boards.</returns>
        public BitArray GetActiveUnitBoards()
        {
            SendCommand(TouchCommandType.GetActiveUnitBoards);
            return new BitArray(ReadData(3).Data);
        }

        public void SetThresholds(byte on, byte off)
        {
            SendData(new byte[] { (byte)TouchCommandType.SetThresholds, 
                on, on, on, on, on, on, // on x6, for each unit board
                off, off, off, off, off, off // off x6, for each unit board
            });

            var status = ReadData(3);

            if (status.Command != (byte)TouchCommandType.SetThresholds)
            {
                throw new InvalidDataException("Set Thresholds message was not acknowledged.");
            } 
            if (status.Data[0] != 0)
            {
                throw new InvalidDataException("Set Thresholds failed!");
            }
            
            _onThreshold = on;
            _offThreshold = off;
        }

        /// <summary>
        /// Instructs the panels to start streaming touch data over the connection.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// Thrown if the <see cref="TouchCommandType.StartAutoScan"/> message was not acknowledged,
        /// something went wrong.
        /// </exception>
        public void StartTouchStream()
        {
            // magic bytes, what do they do?????? who knows.
            SendData(new byte[] { (byte)TouchCommandType.StartAutoScan, 0x7F, 0x3F, 0x64, 0x28, 0x44, 0x3B, 0x3A });
            var ack = ReadData(3); // read ack
            if (ack.Command != (byte)TouchCommandType.StartAutoScan)
                throw new InvalidDataException("Start Scan message was not acknowledged.");
            streamMode = true;
            port.DataReceived += DataReceived;
        }
        
        /// <summary>
        /// Debugging method, retrieves some information about the connected side of the console.
        /// </summary>
        public void DebugInfo()
        {
            Console.WriteLine("TouchManager Info:");
            Console.WriteLine($"Side: {Letter}");
            Console.WriteLine($"Mirrored input: {IsRight}");
            Console.WriteLine($"Sync Board version: {SyncVersion}");
            Console.WriteLine($"Unit Board versions: {string.Join(",",UnitVersions)}");
            if (!streamMode) return;
            Console.WriteLine("===");
            Console.WriteLine($"Loop state: {LoopState}");
            Console.WriteLine($"Currently touched segments: {Segments.Count}");
        }

        /// <summary>
        /// A <see cref="StringBuilder"/> used by <see cref="DebugTouch"/> to increase console performance.
        /// </summary>
        private readonly StringBuilder _debugSb = new StringBuilder();
        
        /// <summary>
        /// Debugging method, intended to be called from a loop to get realtime touch information as it changes.
        /// Outputs a graphic to the console of the current touch state.
        /// </summary>
        /// <remarks>
        /// It is recommended that you disable <see cref="Console.CursorVisible"/> to prevent flickering.
        /// </remarks>
        public void DebugTouch()
        {
            _debugSb.Clear();
            for (byte row = 0; row < 4; row++)
            {
                for (byte column = 0; column < 30; column++)
                {
                    _debugSb.Append(TouchData[row, column] ? "\u2588" : "\u2591");
                }
                _debugSb.Append("\n");
            }
            
            Console.WriteLine("Current Touch Frame:");
            Console.Write(_debugSb.ToString());
            Console.WriteLine($"Loop state: {LoopState,3}");
            Console.WriteLine($"Currently touched segments: {Segments.Count,3}");
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop-7);
        }

        
        /// <summary>
        /// Retrieves the latest touch data from the panels.
        /// The data is also used to update <see cref="touchData"/> every time this is called.
        /// </summary>
        /// <exception cref="Exception">
        /// If read data is not touch data an exception will be thrown.
        /// </exception>
        private void GetTouchData()
        {
            ParseTouchData(ReadData(36));
        }
        
        /// <summary>
        /// Retrieves the latest touch data from the panels.
        /// The data is also used to update <see cref="TouchData"/> every time this is called.
        /// </summary>
        /// <remarks>This method is very temperamental, it cannot handle any data outside what it expects.</remarks>
        /// <returns>The latest touch data in a multi-dimensional array (4x30).</returns>
        /// <exception cref="InvalidDataException">
        /// Thrown if the provided command is not touch data (<see cref="TouchCommandType.TouchData"/>).
        /// </exception>
        private bool[,] ParseTouchData(TouchCommand stream)
        {
            Segments.Clear();
            var raw = stream.Command != 0 ? stream : ReadData(36);
            if (raw.Command != (byte)TouchCommandType.TouchData) throw new InvalidDataException("that's not touch data.");

            // check if we got the same frame twice, exceedingly unlikely.
            // if we did, just return what we have already.
            if (LoopState != raw.Data[raw.Data.Length - 1])
                LoopState = raw.Data[raw.Data.Length - 1];
            else return TouchData;
            
            if(ClearBuffer) Array.Clear(TouchData, 0, TouchData.Length);
            
            Array.Copy(raw.Data, 0, lastRawData, 0, 24);
            
            for (byte row = 0; row < 4; row++)
            {
                for (byte panel = 0; panel < 6; panel++)
                {
                    var rowData = lastRawData[panel + (row * 6)];
                    for (byte segment = 0; segment < 5; segment++)
                    {
                        var active = (rowData & (1 << segment)) != 0;
                        
                        var x = (byte)(segment + (panel * 5));
                        
                        if (active) Segments.Add(new ActiveSegment(x, row));
                        TouchData[row, x] = active;
                    }
                }
            }

            return TouchData;
        } 
        /// <summary>
        /// Sends a command to the Sync Board.
        /// </summary>
        /// <param name="data">The <see cref="TouchCommandType"/> to send.</param>
        private void SendCommand(TouchCommandType data)
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
        /// <exception cref="InvalidDataException">
        /// If the returned data does not have a valid checksum, an exception will be thrown.
        /// </exception>
        private TouchCommand ReadData(int size)
        {
            var raw = new byte[size];
            while (port.BytesToRead < size) {
              
            }
            port.Read(raw, 0, size);
            
            if (!TouchCommand.ValidateChecksum(raw))
            {
                throw new InvalidDataException("Checksum failure!");
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
            if (port.BytesToRead >= 36)
            {
                GetTouchData();
            }
        }
        
        ~SyncBoardController() => Close();
    }

    /// <summary>
    /// A wrapper for a touch board command.
    /// </summary>
    public struct TouchCommand
    {
        public byte Command;
        public byte[] Data;
        public byte Checksum;

        public TouchCommand(byte[] raw)
        {
            Data = new byte[raw.Length - 2];
            Command = raw[0];
            Checksum = raw[raw.Length - 1];
            Array.Copy(raw, 1, Data, 0, Data.Length);
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
            return packet[packet.Length - 1] == chk;
        }

        public static explicit operator TouchCommand(byte[] raw)
        {
            return new TouchCommand(raw);
        }

        public static explicit operator byte[](TouchCommand cmd)
        {
            var raw = new byte[cmd.Data.Length + 2];

            raw[0] = cmd.Command;
            Array.Copy(cmd.Data, 0, raw, 1, cmd.Data.Length);
            raw[raw.Length - 1] = cmd.Checksum;
            
            return raw;
        }
    }
}