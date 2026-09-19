using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

namespace LilyConsole
{
    public class IO4Controller: IDisposable
    {
        public bool Initialized { get; private set; } = false;

        private HidDevice _device;
        private HidStream _stream;
        
        private Task _pollTask;
        private CancellationTokenSource _pollCts;
        
        public bool Polling => _pollTask != null;
        
        private readonly byte[] _readBuffer = new byte[64];
        private readonly byte[] _writeBuffer = new byte[64];
        
        /// <summary>
        /// Get the latest button press information as it happens.
        /// </summary>
        /// <remarks>
        /// This only provides buttons that were pressed in the last report, use <see cref="ButtonStateChanged"/> for
        /// checking if multiple buttons are held down at once.
        /// </remarks>
        public event Action<IO4ButtonState> ButtonPressed;
        
        /// <summary>
        /// Get the latest button release information as it happens.
        /// </summary>
        /// <remarks>
        /// This only provides buttons that were released in the last report, use <see cref="ButtonStateChanged"/> for
        /// checking if multiple buttons are held down at once.
        /// </remarks>
        public event Action<IO4ButtonState> ButtonReleased;
        
        /// <summary>
        /// Get the latest state of the buttons when they change.
        /// </summary>
        /// <remarks>
        ///
        /// </remarks>
        public event Action<IO4ButtonState> ButtonStateChanged;
        
        // TODO: figure out how coin stuff is registered
        public event Action CoinInserted;
        
        public event Action<IO4Report> ReportReceived;
        
        public IO4Report LastReport { get; private set; }
        
        // latest button state, careful using this, written by poll thread
        public IO4ButtonState ButtonState { get; private set; }
        
        private readonly object _writeLock = new object(); 
        
        public void Initialize(bool startPolling = true)
        {
            if (Initialized) return;
            
            _device = DeviceList.Local.GetHidDeviceOrNull(0x0CA3, 0x0021);
            if (_device == null)
            {
                //throw new InvalidOperationException("IO4 HID device not found");
                return;
            }

            var _devConfig = new OpenConfiguration();
            _devConfig.SetOption(OpenOption.Exclusive, true);
            _devConfig.SetOption(OpenOption.Interruptible, true);

            _stream = _device.Open(_devConfig);
            _writeBuffer[0] = 16;
            
            Initialized = true;
            if(startPolling) StartPolling();
        }

        public void Close()
        {
            if(!Initialized || _device == null || _stream == null) return;
            StopPolling();
            ClearColor();
            _stream.Close();
            Initialized = false;
        }
        
        public void SetColor(LightColor color, byte brightness = 0)
        {
            if(!Initialized) return;

            lock (_writeLock)
            {
                Array.Clear(_writeBuffer, 1, _writeBuffer.Length - 1);
            
                _writeBuffer[1] = 0x41; // set unique output

                _writeBuffer[2] = 0b00111000; // set mask to PWM3-5
                _writeBuffer[3] = brightness;
                
                _writeBuffer[6] = color.R; // PWM3
                _writeBuffer[7] = color.G; // PWM4
                _writeBuffer[8] = color.B; // PWM5
                
                _stream.Write(_writeBuffer);
            }
        }

        public void ClearColor()
        {
            if(!Initialized) return;

            lock (_writeLock)
            {
                Array.Clear(_writeBuffer, 1, _writeBuffer.Length - 1);

                _writeBuffer[1] = 0x41; // set unique output

                // everything else is set to 0;

                _stream.Write(_writeBuffer);
            }
        }

        public void StartPolling()
        {
            if(!Initialized || _pollTask != null) return;
            
            _pollCts = new CancellationTokenSource();
            _pollTask = Task.Factory.StartNew(
                () => PollThread(_pollCts.Token),
                _pollCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        private void PollThread(CancellationToken token)
        {
            //Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
            
            while (!token.IsCancellationRequested)
            {
                
                int bytesRead = _stream.Read(_readBuffer, 0, _readBuffer.Length);

                if (bytesRead != _readBuffer.Length) continue;

                if (_readBuffer[0] != 1) throw new InvalidDataException($"Unexpected Report ID 0x{_readBuffer[0]:X}");
                
                LastReport = IO4Report.Build(_readBuffer);
                
                ReportReceived?.Invoke(LastReport);
                
                var oldButtonState = ButtonState;
                ButtonState = (IO4ButtonState)LastReport.buttons[0];
                
                if(ButtonState != oldButtonState) ButtonStateChanged?.Invoke(ButtonState);
                
                var pressedButtons = ButtonState & ~oldButtonState;
                var releasedButtons = ~ButtonState & oldButtonState;
        
                if(pressedButtons != 0) ButtonPressed?.Invoke(pressedButtons);
                if(releasedButtons != 0) ButtonReleased?.Invoke(releasedButtons);
                
                // TODO: test if this actually works how I think it does
                if (LastReport.coin[0] != 0) CoinInserted?.Invoke();
            }
        }

        public void StopPolling()
        {
            if(!Initialized || _pollTask == null) return;
            
            _pollCts.Cancel();
            _pollTask.Wait();
            
            _pollCts.Dispose();
            _pollCts = null;
            _pollTask = null;
        }

        /// <summary>
        /// Debugging method. 
        /// </summary>
        public void ButtonLights()
        {
            switch (ButtonState)
            {
                case IO4ButtonState.VolumeDown:
                    SetColor(LightColor.Red);
                    break;
                case IO4ButtonState.VolumeUp:
                    SetColor(LightColor.Blue);
                    break;
                case IO4ButtonState.Test:
                    SetColor(LightColor.Green);
                    break;
                case IO4ButtonState.Service:
                    SetColor(LightColor.White);
                    break;
                default:
                    ClearColor();
                    break;
            }
        }
        
        public void DebugInfo()
        {
            Console.WriteLine(LastReport);
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop - 7);
        }

        private static void ParseProductName(string name)
        {
            Console.WriteLine(name);
            var parts = name.Split(';');
            if (parts.Length != 8) throw new ArgumentException("Not an IO4 product name!");
            
            // example, not wacca
            // I/O CONTROL BD;BDNUMBER;MD;RV;SUM ;CPNUM;CF;GOUT=14_ADIN=8,E_ROTIN=4_COININ=2_SWIN=2,E_UQ1=41,6;
            
            // var data = new IO4Info()
            // {
            //     boardType = parts[0],
            //     boardNumber = parts[1],
            //     mode = parts[2],
            //     firmwareRev = parts[3],
            //     firmwareChecksum = parts[4],
            //     chipNumber = parts[5],
            //     config = parts[6],
            //     features = parts[7],
            // }
        }

        public void Dispose()
        {
            Close();
        }
    }
}