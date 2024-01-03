using System;
using FTD2XX_NET;

namespace LilyConsole
{
    public class LightController
    {
        // TODO: implement light protocol.
        private FTDI lightBoard = new FTDI();

        private uint deviceCount;
        private FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
        
        public LightController()
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

        public void SendLightFrame(LightFrame frame)
        {
            // TODO: send light frame data here. 
            //lightBoard.Write
        }
    }
}