using System;
using System.Runtime.InteropServices;
using FTD2XX;
using LilyConsole.Helpers;

namespace LilyConsole
{
    public class FTDIController
    {
        public bool Initialized { get; private set; } = false;
        
        private FTDI lightBoard;

        private uint deviceCount;
        private FTDI.FT_STATUS status = FTDI.FT_STATUS.FT_OK;

        private string _error;
        
        public FTDIController()
        {
            
        }

        public string GetLastError()
        {
            return _error;
        }

        public bool Initialize()
        {
            if (Initialized) return true;
            
            lightBoard = new FTDI();
            status = lightBoard.GetNumberOfDevices(ref deviceCount);
            
            if (deviceCount < 1)
            {
                return false;
            }
            
            status = lightBoard.OpenByIndex(0);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                _error = "Failed to open device! Error: " + status;
                return false;
            }

            status = ConfigureMPSSE();
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                _error = "Failed to configure MPSSE! Error: " + status;
                return false;
            }
            
            // Enable SPI communication
            byte[] spiConfig = { 0x8A, 0x97, 0x00 }; // MPSSE command to enable SPI
            uint bytesWritten = 0;
            status = lightBoard.Write(spiConfig, spiConfig.Length, ref bytesWritten);
            if (status != FTDI.FT_STATUS.FT_OK)
            {
                _error = "Failed to enable SPI! Error: " + status;
                return false;
            }
            
            Initialized = true;
            
            return true;
        }
        
        private FTDI.FT_STATUS ConfigureMPSSE()
        {
            status = FTDI.FT_STATUS.FT_OK;
            
            status |= lightBoard.ResetDevice();
            status |= lightBoard.Purge(FTDI.FT_PURGE.FT_PURGE_RX | FTDI.FT_PURGE.FT_PURGE_TX);
            status |= lightBoard.InTransferSize(0x10000);
            status |= lightBoard.SetCharacters(0,false,0,false);
            status |= lightBoard.SetTimeouts(5000, 5000);
            //status |= lightBoard.SetLatency(16);
            status |= lightBoard.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
            status |= lightBoard.SetBitMode(0x00, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);

            return status;
        }

        public bool WriteData(LedData data)
        {
            throw new NotImplementedException("SPI color conversion not implemented yet!");
        }

        public bool WriteData(byte[] data)
        {
            uint bytesSent = 0;

            status = lightBoard.Write(data, (uint)data.Length, ref bytesSent);
            if (status == FTDI.FT_STATUS.FT_OK && bytesSent == data.Length) return true;
            
            _error = "Failed to send SPI data! Error: " + status;
            return false;
        }

        public bool Close()
        {
            if (lightBoard == null) return false;
            status = lightBoard.Close();
            Initialized = false;
            return status == FTDI.FT_STATUS.FT_OK;
        }

        ~FTDIController()
        {
            Close();
        }
    }
}