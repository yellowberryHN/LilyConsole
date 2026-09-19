using System;
using System.Collections.Generic;
using FTD2XX;
using LilyConsole.Helpers;

namespace LilyConsole
{
    public class LightController
    {
        public bool Initialized { get; private set; } = false;
        
        /// <summary>
        /// The handler the light controller is talking to. This is only useful for debugging, usually.
        /// </summary>
        /// <remarks>
        /// Will be set to <see cref="LightHandlerType.Off"/> if the initialization was unable to talk
        /// to the light board in any way, and all light functions will return immediately.
        /// </remarks>
        public LightHandlerType Handler { get; private set; } = LightHandlerType.USBIntLED;

        /// <summary>
        /// The last <see cref="LightFrame"/> sent to the light board.
        /// </summary>
        /// <remarks>
        /// If the last <see cref="LightFrame"/> was sent with <see cref="SendLightFrame(LilyConsole.LightFrame, List{LilyConsole.ActiveSegment})"/>,
        /// this will not include the touch data light information.
        /// </remarks>
        public LightFrame LastFrame { get; private set; } = new LightFrame();

        public readonly static LightFrame BlankFrame = new LightFrame(LightColor.Black);

        private LedData _ledBuffer = LedData.blank;
        
        private FTDIController _ftdi = new FTDIController();

        /// <summary>
        /// Prepares the lights to be controlled.
        /// </summary>
        /// <remarks>You only really need to call this once.</remarks>
        /// <returns>The success state of the initialization.</returns>
        public bool Initialize()
        {
            if (Initialized) return true;
            
            if (USBIntLED.Safe_USBIntLED_Init()) return Initialized = true;
            
            Handler = LightHandlerType.FTD2XX;
            // TODO: uncomment when SPI light conversion code written
            //if (_ftdi.Initialize()) return Initialized = true;
            
            Handler = LightHandlerType.Off;
            return false;
        }

        /// <summary>
        /// Cleans up the light board, sets all the lights to off, and terminates the connection.
        /// </summary>
        /// <param name="clear">Whether to turn off the lights while cleaning up.</param>
        /// <remarks>If you call this, you must call <see cref="Initialize"/> again if you want to talk to the board again.</remarks>
        /// <returns>The success state of the cleanup.</returns>
        public bool Close(bool clear = true)
        {
            if (Handler == LightHandlerType.Off) return true;

            if (clear) Clear();
            
            Initialized = false;
            
            switch (Handler)
            {
                case LightHandlerType.USBIntLED:
                    return USBIntLED.Safe_USBIntLED_Terminate();
                case LightHandlerType.FTD2XX:
                    return _ftdi.Close();
                default:
                    throw new NotSupportedException("Handler not supported");
            }
        }

        /// <summary>
        /// Sends a <see cref="LightFrame"/> to the light board immediately.
        /// </summary>
        /// <param name="frame">The frame to send.</param>
        public void SendLightFrame(LightFrame frame)
        {
            if (Handler == LightHandlerType.Off) return;
            
            frame.Flatten(ref _ledBuffer.rgbaValues);
            
            switch (Handler)
            {
                case LightHandlerType.USBIntLED:
                    USBIntLED.Safe_USBIntLED_set(0, _ledBuffer);
                    break;
                case LightHandlerType.FTD2XX:
                    _ftdi.WriteData(_ledBuffer);
                    break;
                default:
                    throw new NotSupportedException("Handler not supported");
            }
            
            LastFrame = frame;
        }

        /// <summary>
        /// Sends a <see cref="LightFrame"/> to the light board,
        /// first compositing the currently active segments onto it.
        /// </summary>
        /// <param name="frame">The frame to send.</param>
        /// <param name="segments">A list of segments which are currently active.</param>
        public void SendLightFrame(LightFrame frame, List<ActiveSegment> segments)
        {
            if (Handler == LightHandlerType.Off) return;
            
            frame.AddTouchData(segments);
            
            frame.Flatten(ref _ledBuffer.rgbaValues);
            
            switch (Handler)
            {
                case LightHandlerType.USBIntLED:
                    USBIntLED.Safe_USBIntLED_set(0, _ledBuffer);
                    break;
                case LightHandlerType.FTD2XX:
                    // TODO: try to do FTD2XX stuff here
                    break;
                default:
                    throw new NotSupportedException("Handler not supported");
            }
            
            LastFrame = frame;
        }

        public void Clear()
        {
            // special frame required because lights do not respond to request to change to (0,0,0,0)
            SendLightFrame(BlankFrame);
        }

        /// <summary>
        /// Cleans up when the light controller is garbage collected.
        /// </summary>
        ~LightController()
        {
            Close();
        }
    }

    public enum LightHandlerType {
        Off,
        FTD2XX,
        USBIntLED,
    }
}