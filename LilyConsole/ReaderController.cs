using System;
using System.IO.Ports;
#if UNITY
using UnityEngine;
#endif

namespace LilyConsole
{
    // https://github.com/whowechina/aic_pico/blob/main/firmware/src/lib/aime.c
    public class ReaderController
    {
        private SerialPort port;
        private byte sequence_id;
        
        #if UNITY
        public Color32 reader_color { get; private set; }
        #else
        public LightColor reader_color { get; private set; }
        #endif
        
        private string firmware_version = string.Empty;
        private string hardware_version = string.Empty;
        
        public ReaderController(string portName = "COM1")
        {
            throw new NotImplementedException("This feature is not yet implemented");
            
            // TODO: check for both reader speeds, somehow
            port = new SerialPort(portName, 115200);
        }

        public void Initialize()
        {
            port.Open();
        }
        
        private void SendData(byte[] data)
        {
            port.Write(data, 0, data.Length);
            sequence_id++;
        }

        #if UNITY
        public void SetColor(Color32 color)
        #else
        public void SetColor(LightColor color)
        #endif
        {
            reader_color = color;
            SendData(new byte[] { 0x08, 0x00, sequence_id, 0x81, 0x03, (byte)color.r, (byte)color.g, (byte)color.b });
        }

        public void SetColor(byte r, byte g, byte b) 
        {
            #if UNITY
            SetColor(new Color32(r,g,b,0xFF));
            #else
            SetColor(new LightColor(r,g,b));
            #endif
        }

        public void GetFirmwareVersion()
        {
            throw new NotImplementedException();
        }
    }
}