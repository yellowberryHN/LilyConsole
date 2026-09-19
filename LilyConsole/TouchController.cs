using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace LilyConsole
{
    public class TouchController
    {
        public bool Initialized => LeftSide.Initialized && RightSide.Initialized;

        /// <summary>
        /// Manager for the left side of the console.
        /// </summary>
        public SyncBoardController LeftSide;
        /// <summary>
        /// Manager for the right side of the console.
        /// </summary>
        /// <remarks>
        /// Please note that this controller is <see cref="SyncBoardController.Mirrored"/>, due
        /// to being marked as <see cref="SyncBoardController.Normalized"/>.
        /// </remarks>
        public SyncBoardController RightSide;

        private readonly string _leftPort;
        private readonly string _rightPort;

        public SyncBoardThresholds Thresholds = SyncBoardThresholds.Defaults();
        
        public event Action<List<ActiveSegment>> DataReceived;

        public event Action<List<ActiveSegment>> TouchChanged;
        public event Action<List<ActiveSegment>> TouchStarted;
        public event Action<List<ActiveSegment>> TouchEnded;

        /// <summary>
        /// The last retrieved touch information as a multidimensional array (4x60).
        /// <br/><br/>
        /// IMPORTANT: <see cref="TouchController.TouchData"/> is accessed [Y,X], <see cref="ActiveSegment"/> is addressed (X,Y)! 
        /// </summary>
        public readonly bool[,] TouchData = new bool[4, 60];
        
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public readonly List<ActiveSegment> Segments = new List<ActiveSegment>();
        private readonly List<ActiveSegment> _lSegments = new List<ActiveSegment>();
        private readonly List<ActiveSegment> _rSegments = new List<ActiveSegment>();
        
        

        /// <summary>
        /// Creates a new touch controller interface. This does not attempt communications with the console until
        /// <see cref="Initialize"/> is called.
        /// </summary>
        /// <param name="leftPort">The name passed to <see cref="SerialPort"/> for the left side of the console.</param>
        /// <param name="rightPort">The name passed to <see cref="SerialPort"/> for the right side of the console.</param>
        public TouchController(string leftPort = "COM4", string rightPort = "COM3")
        {
            _leftPort = leftPort;
            _rightPort = rightPort;
        }

        /// <summary>
        /// Creates a new connection to both sides of the console.
        /// </summary>
        /// <exception cref="System.IO.IOException">Will be thrown if serial port was not found.</exception>
        public void Initialize()
        {
            LeftSide = new SyncBoardController(_leftPort, SyncBoardSide.Left) { Thresholds = this.Thresholds, Normalized = true };
            RightSide = new SyncBoardController(_rightPort, SyncBoardSide.Right) { Thresholds = this.Thresholds, Normalized = true };
            
            LeftSide.DataReceived += OnTouchData;
            RightSide.DataReceived += OnTouchData;

            LeftSide.TouchChanged += OnTouchChanged;
            RightSide.TouchChanged += OnTouchChanged;

            LeftSide.TouchStarted += OnTouchStarted;
            RightSide.TouchStarted += OnTouchStarted;
            
            LeftSide.Initialize();
            RightSide.Initialize();
        }

        private void OnTouchData(List<ActiveSegment> segments, SyncBoardSide side)
        {
            _lSegments.Clear();
            _lSegments.AddRange(segments);
            _rSegments.Clear();
            _rSegments.AddRange(segments);
            MergeSegments();
        }

        private void OnTouchChanged(List<ActiveSegment> segments, SyncBoardSide side)
        {
            TouchChanged?.Invoke(Segments);
        }

        private void OnTouchStarted(List<ActiveSegment> segments, SyncBoardSide side)
        {
            
        }

        private void MergeSegments()
        {
            Segments.Clear();
            Segments.AddRange(_lSegments);
            Segments.AddRange(_rSegments);
            
            DataReceived?.Invoke(Segments);
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// </summary>
        public void Close()
        {
            LeftSide.Close();
            RightSide.Close();
        }

        /// <summary>
        /// Instructs all panels to start transmitting touch data.
        /// </summary>
        public void StartPolling()
        {
            LeftSide.StartPolling();
            RightSide.StartPolling();
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
            Console.WriteLine($"Loop state: L: {LeftSide.LoopState,3}, R: {RightSide.LoopState,3}");
            Console.WriteLine($"Currently touched segments: {Segments.Count,3}");
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 7);
        }
    }

    public class SyncBoardController
    {
        public bool Initialized { get; private set; } = false;
        
        private SerialPort port;
        private string portName;

        private Task _pollTask;
        private CancellationTokenSource _pollCts;

        public bool Polling => _pollTask != null;
        
        private readonly byte[] _readBuffer = new byte[256];
        private readonly byte[] _writeBuffer = new byte[256];

        private TouchCommandType _touchType = TouchCommandType.TouchData;
        
        public SyncBoardThresholds Thresholds = SyncBoardThresholds.Defaults();
        
        public event Action<List<ActiveSegment>, SyncBoardSide> DataReceived;

        public event Action<List<ActiveSegment>, SyncBoardSide> TouchChanged;
        public event Action<List<ActiveSegment>, SyncBoardSide> TouchStarted;
        public event Action<List<ActiveSegment>, SyncBoardSide> TouchEnded;

        /// <summary>
        /// Whether the data should be normalized to the coordinate plane of the entire console.
        /// This effectively mirrors the X axis on the input from right side.
        /// </summary>
        public bool Normalized;
        
        /// <summary>
        /// Used to determine if data is presented with the X axis mirrored.
        /// </summary>
        public bool Mirrored => Normalized && Side == SyncBoardSide.Right;
        
        /// <summary>
        /// The version string of the Sync Board.
        /// </summary>
        public string SyncVersion = string.Empty;
        /// <summary>
        /// The version strings of all 6 Unit Boards, present in each of the 6 panels.
        /// Probably not a good sign if these don't match.
        /// </summary>
        public readonly string[] UnitVersions = new string[6];
        
        private readonly byte[] lastRawData = new byte[24];
        
        public bool ThrowOnChecksumError = true;
        
        /// <summary>
        /// The last retrieved touch information as a multidimensional array (4x30).
        /// The coordinates are relative to the inner top corner of their side being 0,0
        /// <br/><br/>
        /// IMPORTANT: <see cref="SyncBoardController.TouchData"/> is accessed [Y,X], <see cref="ActiveSegment"/> is addressed (X,Y)! 
        /// </summary>
        /// <remarks>Potentially subject to race conditions, depending on how you set up your touch polling.</remarks>
        public readonly bool[,] TouchData = new bool[4,30];
        private readonly bool[,] _prevTouchData = new bool[4,30];
        
        
        /// <summary>
        /// The last retrieved touch information as a list of coordinates.
        /// </summary>
        public readonly List<ActiveSegment> Segments = new List<ActiveSegment>();
        private readonly List<ActiveSegment> _prevSegments = new List<ActiveSegment>();
        
        public byte LoopState = 0;

        /// <summary>
        /// The identifier of which side of the console this is.
        /// </summary>
        public readonly SyncBoardSide Side;
        
        /// <param name="portName">The name passed to <see cref="SerialPort"/> for the specified side of the console.</param>
        /// <param name="side">The letter code of the side. Must be 'L' or 'R'.</param>
        /// <exception cref="ArgumentException">Will be thrown if the letter code is not 'L' or 'R'.</exception>
        public SyncBoardController(string portName, SyncBoardSide side)
        {
            if (side != SyncBoardSide.Left && side != SyncBoardSide.Right)
            {
                throw new ArgumentException($"Side {side} is unknown to TouchManager.");
            }
            Side = side;
            
            this.portName = portName;
        }

        /// <summary>
        /// Creates a new connection to this side of the console.
        /// </summary>
        /// <exception cref="System.IO.IOException">Will be thrown if serial port was not found.</exception>
        public void Initialize()
        {
            if (Initialized) return;
            
            port = new SerialPort(portName, 115200);
            port.ReadTimeout = 0;
            
            port.Open();
            ShutUpPlease();
            GetSyncVersion();
            GetUnitVersion();
            Initialized = true;
            
            GetActiveUnitBoards();
            SetThresholds();
        }

        /// <summary>
        /// Closes the connection to the console, ending any data transfer.
        /// Flushes out any data that remains.
        /// </summary>
        /// <remarks>This does not do anything if the connection is not open, to prevent weird states.</remarks>
        public void Close()
        {
            if (!Initialized) return;
            StopTouchStream();
            Array.Clear(TouchData, 0, TouchData.Length);
            Array.Clear(_prevTouchData, 0, _prevTouchData.Length);
            Segments.Clear();
            _prevSegments.Clear();
            LoopState = 0;
            SyncVersion = string.Empty;
            Array.Clear(UnitVersions, 0, UnitVersions.Length);
            port.Close();

            Initialized = false;
        }

        /// <summary>
        /// <b>THIS IS A HACK.</b> We can get the sync board to stop streaming data by asking it for the sync board
        /// version a bunch of times and then waiting a bit. This is certainly not a good solution, but it works.
        /// </summary>
        private void ShutUpPlease()
        {
            if(_pollTask != null) return;
            
            port.DiscardInBuffer();
            for (var i = 0; i < 5; i++)
            {
                SendCommand(TouchCommandType.GetSyncBoardVersion);
                port.DiscardInBuffer();
            }
            Thread.Sleep(20);
            port.DiscardInBuffer();
        }
        
        /// <summary>
        /// Asks the Sync Board to return its version string.
        /// As a consequence of sending this command, all touch data communications are halted if they are being sent.
        /// </summary>
        private void GetSyncVersion()
        {
            if (_pollTask != null) return;
            
            var cmd = RequestCommand(TouchCommandType.GetSyncBoardVersion);
            SyncVersion = Encoding.ASCII.GetString(cmd.Data);
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
            if (_pollTask != null) return;
            
            var cmd = RequestCommand(TouchCommandType.GetUnitBoardVersion);
            var info = Encoding.ASCII.GetString(cmd.Data);
            SyncVersion = info.Substring(0, 6);
            if (info[6] != Side.ToString()[0]) throw new InvalidDataException("Sync Board disagrees which side it is!");
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
            if (!Initialized || _pollTask != null) return new BitArray(8);
            
            var cmd = RequestCommand(TouchCommandType.GetActiveUnitBoards);
            return new BitArray(cmd.Data);
        }

        /// <summary>
        /// Set the activation thresholds for the touch panels. This currently doesn't work.
        /// </summary>
        /// <param name="on">If the raw sensor reading increases above this number, the segment will be turned ON.</param>
        /// <param name="off">If the raw sensor reading decreases below this number, the segment will be turned OFF.</param>
        /// <exception cref="InvalidDataException">Something went wrong.</exception>
        public void SetThresholds()
        {
            if (!Initialized || _pollTask != null) return;

            var on = Thresholds.OnThreshold;
            var off = Thresholds.OffThreshold;
            
            SendCommand(TouchCommandType.SetThresholds, new [] {
                on, on, on, on, on, on, // on x6, for each unit board
                off, off, off, off, off, off // off x6, for each unit board
            });

            var status = ReadCommand(TouchCommandType.SetThresholds);

            if (status.Command != (byte)TouchCommandType.SetThresholds)
            {
                throw new InvalidDataException("Set Thresholds message was not acknowledged.");
            } 
            if (status.Data[0] != 0)
            {
                throw new InvalidDataException("Set Thresholds failed!");
            }
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
        /// Sends a command to the Sync Board.
        /// </summary>
        /// <param name="command">The <see cref="TouchCommandType"/> to send.</param>
        private void SendCommand(TouchCommandType command)
        {
            SendData(new[]{(byte)command});
        }

        /// <summary>
        /// Sends a command to the Sync Board, with specified data. A checksum byte is appended for you.
        /// </summary>
        /// <param name="command">The <see cref="TouchCommandType"/> to send.</param>
        /// <param name="data">The data to send.</param>
        private void SendCommand(TouchCommandType command, byte[] data)
        {
            var combined = new byte[data.Length + 2];
            data.CopyTo(combined, 1);
            combined[0] = (byte)command;
            combined[data.Length + 1] = TouchCommand.CalculateChecksum(combined);
            
            SendData(combined);
        }
        
        /// <summary>
        /// Reads returned data from a command sent to the Sync Board.
        /// </summary>
        /// <param name="type">The type of command that was sent</param>
        /// <returns>The returned data, as a <see cref="TouchCommand"/>.</returns>
        /// <exception cref="InvalidDataException">
        /// If the returned data does not have a valid checksum, an exception will be thrown.
        /// If <see cref="ThrowOnChecksumError"/> is false, invalid checksums will be ignored.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// If the command return size is unknown, an exception will be thrown.
        /// </exception>
        private TouchCommand ReadCommand(TouchCommandType type)
        {
            if (!TouchCommand.ReadSize.TryGetValue(type, out var cmdSize))
            {
                throw new NotSupportedException("Command size not known!");
            }
            
            port.Read(_readBuffer, 0, cmdSize);
            
            if (!TouchCommand.ValidateChecksum(_readBuffer, cmdSize) && ThrowOnChecksumError)
            {
                throw new InvalidDataException("Checksum failure!");
            }

            return new TouchCommand(_readBuffer);
        }

        private TouchCommand RequestCommand(TouchCommandType type)
        {
            SendCommand(type);
            return ReadCommand(type);
        }

        private TouchCommand RequestCommand(TouchCommandType type, byte[] data)
        {
            SendCommand(type, data);
            return ReadCommand(type);
        }
        
        private readonly List<ActiveSegment> _segmentsTouchStart = new List<ActiveSegment>();
        private readonly List<ActiveSegment> _segmentsTouchEnd = new List<ActiveSegment>();

        private bool ReadTouchData()
        {
            _segmentsTouchStart.Clear();
            _segmentsTouchEnd.Clear();

            var touchSize = TouchCommand.ReadSize[_touchType];
            port.Read(_readBuffer, 0, touchSize);
            
            if (_readBuffer[0] != (byte)_touchType)
            {
                Array.Clear(TouchData, 0, TouchData.Length);
                throw new ArgumentException("that's not touch data.");
            }

            var newLoopState = _readBuffer[touchSize - 2];

            if (LoopState != newLoopState) LoopState = newLoopState;
            else return false; // no new data
            
            Buffer.BlockCopy(_readBuffer, 1, lastRawData, 0, 24);
            
            Buffer.BlockCopy(TouchData, 0, _prevTouchData, 0, TouchData.Length);
            Array.Clear(TouchData, 0, TouchData.Length);

            _prevSegments.AddRange(Segments);
            Segments.Clear();
            
            for (byte row = 0; row < 4; row++)
            {
                for (byte panel = 0; panel < 6; panel++)
                {
                    var rowData = lastRawData[panel + (row * 6)];
                    for (byte segment = 0; segment < 5; segment++)
                    {
                        var active = (rowData & (1 << segment)) != 0;
                        
                        var tX = (byte)(segment + (panel * 5));
                        var sX = tX;
                        
                        if (Mirrored)
                        {
                            tX = (byte)(29 - tX);
                            sX = (byte)(30 + tX);
                        }

                        if (active)
                        {
                            Segments.Add(new ActiveSegment(sX, row));
                            TouchData[row, tX] = true;
                        }

                        switch (TouchData[row, tX])
                        {
                            case true when !_prevTouchData[row, tX]:
                                _segmentsTouchStart.Add(new ActiveSegment(sX, row));
                                break;
                            case false when _prevTouchData[row, tX]:
                                _segmentsTouchEnd.Add(new ActiveSegment(sX, row));
                                break;
                        }
                    }
                }
            }

            return true;
        }
        
        /// <summary>
        /// Instructs the panels to start streaming touch data over the connection.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// Thrown if the <see cref="TouchCommandType.StartAutoScan"/> message was not acknowledged,
        /// something went wrong.
        /// </exception>
        public void StartPolling(bool analog = false)
        {
            if (!Initialized || _pollTask != null) return;
            
            // TODO: add support for StartAutoScanChatter and StartAutoScanGap (not really high priority)
            
            var commandType = analog ? TouchCommandType.StartAutoScanAnalog : TouchCommandType.StartAutoScan;

            if (!analog)
            {
                // these parameters are not super well understood, but they are technically configurable.
                SendCommand(commandType, new[]
                {
                    Thresholds.SwitchGapSumThreshold,
                    Thresholds.SwitchGapSoloThreshold,
                    Thresholds.UnitGapSumThreshold,
                    Thresholds.UnitGapSoloThreshold,
                    Thresholds.SwitchGapOffThreshold, 
                    Thresholds.UnitGapOffThreshold
                });
            }
            else
            {
                SendCommand(commandType);
            }
            var ack = ReadCommand(commandType); // read ack
            if (ack.Command != (byte)commandType)
                throw new InvalidDataException("Start Scan message was not acknowledged.");

            _touchType = analog ? TouchCommandType.TouchDataAnalog : TouchCommandType.TouchData;
            
            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Factory.StartNew(
                () => PollThread(_pollCts.Token),
                _pollCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void PollThread(CancellationToken token)
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            while (!token.IsCancellationRequested)
            {
                if (!ReadTouchData()) continue;

                // this won't get called if loop state is identical after 2 calls, does this make sense?
                DataReceived?.Invoke(Segments, Side);
                
                if (_segmentsTouchStart.Any() || _segmentsTouchEnd.Any())
                    TouchChanged?.Invoke(Segments, Side);
                if (_segmentsTouchStart.Any())
                    TouchStarted?.Invoke(_segmentsTouchStart, Side);
                if (_segmentsTouchEnd.Any())
                    TouchEnded?.Invoke(_segmentsTouchEnd, Side);
            }
        }

        public void StopTouchStream()
        {
            if (!Initialized || _pollTask == null) return;
            
            _pollCts.Cancel();
            _pollTask.Wait();
            
            _pollCts.Dispose();
            _pollCts = null;
            _pollTask = null;
            
            ShutUpPlease();
        }
        
        /// <summary>
        /// Debugging method, retrieves some information about the connected side of the console.
        /// </summary>
        public void DebugInfo()
        {
            Console.WriteLine("TouchManager Info:");
            Console.WriteLine($"Side: {Side}");
            Console.WriteLine($"Mirrored input: {Mirrored}");
            Console.WriteLine($"Sync Board version: {SyncVersion}");
            Console.WriteLine($"Unit Board versions: {string.Join(",",UnitVersions)}");
            if (_pollTask == null) return;
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
    }
}