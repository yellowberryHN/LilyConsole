using System;
using System.Collections.Generic;
using FTD2XX;
using LilyConsole.Helpers;

namespace LilyConsole
{
    public class LightController
    {
        /// <summary>
        /// The handler the light controller is talking to. This is only useful for debugging, usually.
        /// </summary>
        /// <remarks>
        /// Will be set to <see cref="LightHandlerType.Off"/> if the initialization was unable to talk
        /// to the light board in any way, and all light functions will return immediately.
        /// </remarks>
        public LightHandlerType handler { get; private set; } = LightHandlerType.USBIntLED;

        /// <summary>
        /// The last <see cref="LightFrame"/> sent to the light board.
        /// </summary>
        /// <remarks>
        /// If the last <see cref="LightFrame"/> was sent with <see cref="SendLightFrame(LilyConsole.LightFrame, List{LilyConsole.ActiveSegment})"/>,
        /// this will not include the touch data light information.
        /// </remarks>
        public LightFrame lastFrame { get; private set; } = new LightFrame();
        
        public LightController()
        {
            
        }

        /// <summary>
        /// Prepares the lights to be controlled.
        /// </summary>
        /// <remarks>You only really need to call this once.</remarks>
        /// <returns>The success state of the initialization.</returns>
        public bool Initialize()
        {
            if(USBIntLED.Safe_USBIntLED_Init()) return true;
            
            handler = LightHandlerType.FTD2XX;
            // TODO: try to do FTD2XX stuff here
            if(false) return true;

            handler = LightHandlerType.Off;
            return false;
        }

        /// <summary>
        /// Cleans up the light board, sets all the lights to off, and terminates the connection.
        /// </summary>
        /// <remarks>If you call this, you must call <see cref="Initialize"/> again if you want to talk to the board again.</remarks>
        /// <returns>The success state of the cleanup.</returns>
        public bool CleanUp()
        {
            if (handler == LightHandlerType.Off) return true;
            
            switch (handler)
            {
                case LightHandlerType.USBIntLED:
                    return USBIntLED.Safe_USBIntLED_Terminate();
                case LightHandlerType.FTD2XX:
                    return false;
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
            if (handler == LightHandlerType.Off) return;
            
            lastFrame = frame;
            
            switch (handler)
            {
                case LightHandlerType.USBIntLED:
                    USBIntLED.Safe_USBIntLED_set(0, (LedData)frame);
                    break;
                case LightHandlerType.FTD2XX:
                    // TODO: try to do FTD2XX stuff here
                    break;
                default:
                    throw new NotSupportedException("Handler not supported");
            }
        }

        /// <summary>
        /// Sends a <see cref="LightFrame"/> to the light board,
        /// first compositing the currently active segments onto it.
        /// </summary>
        /// <param name="frame">The frame to send.</param>
        /// <param name="segments">A list of segments which are currently active.</param>
        public void SendLightFrame(LightFrame frame, List<ActiveSegment> segments)
        {
            if (handler == LightHandlerType.Off) return;
            
            lastFrame = frame;
            
            frame.AddTouchData(segments);
            
            switch (handler)
            {
                case LightHandlerType.USBIntLED:
                    USBIntLED.Safe_USBIntLED_set(0, (LedData)frame);
                    break;
                case LightHandlerType.FTD2XX:
                    // TODO: try to do FTD2XX stuff here
                    break;
                default:
                    throw new NotSupportedException("Handler not supported");
            }
        }

        /// <summary>
        /// Cleans up when the light controller is garbage collected.
        /// </summary>
        ~LightController()
        {
            CleanUp();
        }
    }

    public enum LightHandlerType {
        Off,
        FTD2XX,
        USBIntLED,
    }
    
    // TODO: rework this into the new code above
    
    [Obsolete]
    class OldLightController
    {
        // TODO: implement light protocol.
        private FTDI lightBoard = new FTDI();

        private uint deviceCount;
        private FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
        
        private OldLightController()
        {
            try
            {
                ftStatus = lightBoard.GetNumberOfDevices(ref deviceCount);
            }
            catch
            {
                throw new Exception("D2XX driver not loaded");
            }
        }

        public bool Initialize()
        {
            if (deviceCount < 1)
            {
                Console.WriteLine("Failed to find any devices!");
                return false;
            }
            
            ftStatus = lightBoard.OpenByIndex(0);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine("Failed to open device! Error: " + ftStatus.ToString());
                return false;
            }

            ftStatus = ConfigureMPSSE();
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine("Failed to configure MPSSE! Error: " + ftStatus.ToString());
                return false;
            }
            
            // Enable SPI communication
            byte[] spiConfig = { 0x8A, 0x97, 0x00 }; // MPSSE command to enable SPI
            uint bytesWritten = 0;
            ftStatus = lightBoard.Write(spiConfig, spiConfig.Length, ref bytesWritten);
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                Console.WriteLine("Failed to enable SPI! Error: " + ftStatus.ToString());
                return false;
            }
            
            return true;
        }

        public void Close()
        {
            lightBoard.Close();
        }

        public bool WriteData(byte[] data)
        {
            uint bytesSent = 0;

            ftStatus = lightBoard.Write(data, (uint)data.Length, ref bytesSent);
            if (ftStatus == FTDI.FT_STATUS.FT_OK && bytesSent == data.Length) return true;
            
            Console.WriteLine("Failed to send SPI data! Error: " + ftStatus.ToString());
            return false;
        }

        private FTDI.FT_STATUS ConfigureMPSSE()
        {
            ftStatus = FTDI.FT_STATUS.FT_OK;
            
            ftStatus |= lightBoard.ResetDevice();
            ftStatus |= lightBoard.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
            ftStatus |= lightBoard.InTransferSize(0x10000);
            ftStatus |= lightBoard.SetCharacters(0,false,0,false);
            ftStatus |= lightBoard.SetTimeouts(5000, 5000);
            //ftStatus |= lightBoard.SetLatency(16);
            ftStatus |= lightBoard.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
            ftStatus |= lightBoard.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);

            return ftStatus;
        }
    }
}