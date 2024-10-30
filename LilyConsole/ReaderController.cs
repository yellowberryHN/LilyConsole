using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Ports;
using System.Text;
#if UNITY
using UnityEngine;
#endif

namespace LilyConsole
{
    // https://github.com/whowechina/aic_pico/blob/main/firmware/src/lib/aime.c
    public class ReaderController
    {
        private SerialPort port;
        private byte currentSeq;
        
        public bool DebugMode;
        
        public bool radioEnabled { get; private set; }
        public bool ready { get; private set; }
        
        #if UNITY
        private Color32 _readerColor;
        public Color32 readerColor { get => _readerColor; set => SetColor(value); }
        #else
        private LightColor _readerColor;
        public LightColor readerColor { get => _readerColor; set => SetColor(value); }
        #endif
        
        public byte firmwareVersion { get; private set; }
        public string hardwareVersion { get; private set; }

        private List<ReaderCard> _lastPoll = new List<ReaderCard>();
        public ReadOnlyCollection<ReaderCard> lastPoll => _lastPoll.AsReadOnly();
        
        public ReaderController(string portName = "COM1", bool highRate = true)
        {
            port = new SerialPort(portName, highRate ? 115200 : 38400);
        }

        /// <summary>
        /// Creates a new connection to the card reader, and sets it up for use.
        /// </summary>
        /// <remarks>This does not do anything if the connection is already open.</remarks> 
        public void Initialize()
        {
            if (port.IsOpen) return;
            port.Open();
            Reset();
            GetFirmwareVersion();
            GetHardwareVersion();
            SetDefaultKeys();
        }

        /// <summary>
        /// Closes the connection to the reader, ending any data transfer.
        /// </summary>
        /// <remarks>This does not do anything if the connection is not open, to prevent weird states.</remarks>
        public void Close()
        {
            if (!port.IsOpen) return;
            ClearColor();
            RadioOff();
            port.Close();
        }

        #region Command Wrappers
        
        /// <summary>
        /// Enables the NFC radio, which makes it possible to poll and read cards in the field.
        /// </summary>
        /// <param name="type">What type of cards should we support reading. Default is both types.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        /// <seealso cref="RadioOff()"/>
        public ReaderResponseStatus RadioOn(ReaderCardType type = ReaderCardType.Mifare | ReaderCardType.FeliCa)
        {
            SendCommand(new ReaderCommand(ReaderCommandType.RadioOn, new []{ (byte)type }));
            var resp = GetResponse();

            radioEnabled = true;
            
            return resp.status;
        }

        /// <summary>
        /// Disables the NFC radio. You probably don't ever need to do this.
        /// </summary>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        /// <seealso cref="RadioOn(ReaderCardType)"/>
        public ReaderResponseStatus RadioOff()
        {
            SendCommand(new ReaderCommand(ReaderCommandType.RadioOff));
            var resp = GetResponse();

            radioEnabled = false;
            
            return resp.status;
        }

        /// <summary>
        /// Gets a list of all the cards within range of the reader.
        /// </summary>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="InvalidDataException">Thrown if the returned data is malformed in some way.</exception>
        /// <remarks>Do not call this too quickly, it will start returning errors. Not recommended to call it faster than every 150ms.</remarks>
        public ReaderResponseStatus Poll()
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            _lastPoll.Clear();
            SendCommand(new ReaderCommand(ReaderCommandType.CardPoll));
            var resp = GetResponse();

            if (resp.payload.Length == 0) return resp.status;

            using (var reader = new BinaryReader(new MemoryStream(resp.payload)))
            {
                var cardCount = reader.ReadByte();
                for (var i = 0; i < cardCount; i++)
                {
                    var cardType = reader.ReadByte();
                    var uidLen = reader.ReadByte();
                    switch (cardType)
                    {
                        case 0x10: // Mifare
                            _lastPoll.Add(new ReaderCard(ReaderCardType.Mifare, reader.ReadBytes(uidLen)));
                            break;
                        case 0x20: // FeliCa
                            if (uidLen != 16) throw new InvalidDataException("Invalid FeliCa UID length");
                            _lastPoll.Add(new ReaderCard(ReaderCardType.FeliCa, reader.ReadBytes(uidLen)));
                            break;
                        default:
                            throw new InvalidDataException($"Unknown card type: {cardType}");
                    }
                }
            }
            
            return resp.status;
        }

        /// <summary>
        /// Select the card to communicate with.
        /// You must do this before you can talk to a specific card.
        /// </summary>
        /// <param name="uid">The UID of the card you want to select.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>This method is only for selecting Mifare cards.</remarks>
        /// <seealso cref="AuthenticateKeyA(byte[],byte)"/>
        /// <seealso cref="AuthenticateKeyB(byte[],byte)"/>
        public ReaderResponseStatus SelectCard(byte[] uid)
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            var command = uid.Length == 4 ? ReaderCommandType.MifareSelectCard : ReaderCommandType.MifareSelectCardLong;
            SendCommand(new ReaderCommand(command, uid));
            var resp = GetResponse();

            return resp.status;
        }

        /// <summary>
        /// Select the card to communicate with.
        /// You must do this before you can talk to a specific card.
        /// </summary>
        /// <param name="card">The card you want to select.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <remarks>This method is only for selecting Mifare cards.</remarks>
        /// <seealso cref="AuthenticateKeyA(byte[],byte)"/>
        /// <seealso cref="AuthenticateKeyB(byte[],byte)"/>
        public ReaderResponseStatus SelectCard(ReaderCard card)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("SelectCard is only for Mifare cards");
            
            return SelectCard(card.uid);
        }

        /// <summary>
        /// Sets the reader's Key A to the provided key.
        /// It will be used for subsequent calls to <see cref="AuthenticateKeyA(byte[],byte)"/>.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is not exactly 6 bytes long.</exception>
        /// <seealso cref="AuthenticateKeyA(byte[],byte)"/>
        /// <seealso cref="SetKeyB(byte[])"/>
        public ReaderResponseStatus SetKeyA(byte[] key)
        {
            if(key.Length != 6) throw new ArgumentException("Invalid key A length");
            
            SendCommand(new ReaderCommand(ReaderCommandType.MifareSetKeyA, key));
            var resp = GetResponse();
            
            return resp.status;
        }
        
        /// <summary>
        /// Sets the reader's Key B to the provided key.
        /// It will be used for subsequent calls to <see cref="AuthenticateKeyB(byte[],byte)"/>.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is not exactly 6 bytes long.</exception>
        /// <seealso cref="AuthenticateKeyB(byte[],byte)"/>
        /// <seealso cref="SetKeyA(byte[])"/>
        public ReaderResponseStatus SetKeyB(byte[] key)
        {
            if(key.Length != 6) throw new ArgumentException("Invalid key B length");
            
            SendCommand(new ReaderCommand(ReaderCommandType.MifareSetKeyB, key));
            var resp = GetResponse();
            
            return resp.status;
        }

        /// <summary>
        /// Select a card and authenticate against it using the reader's Key A.
        /// Used to gain permission to perform operations (read/write) on the blocks in a card sector.
        /// </summary>
        /// <param name="uid">The UID of the card you want to authenticate against.</param>
        /// <param name="block">The block to authenticate against. Must be a trailer block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>You must authenticate against the trailer block for every sector you wish to perform operations on.</remarks>
        /// <seealso cref="SetKeyA(byte[])"/>
        public ReaderResponseStatus AuthenticateKeyA(byte[] uid, byte block)
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            var data = new byte[5];
            Array.Copy(uid, 0, data, 0, 4);
            data[4] = block;
            
            SendCommand(new ReaderCommand(ReaderCommandType.MifareAuthKeyA, data));
            var resp = GetResponse();
            
            return resp.status;
        }

        /// <summary>
        /// Select a card and authenticate against it using the reader's Key A.
        /// Used to gain permission to perform operations (read/write) on the blocks in a card sector.
        /// </summary>
        /// <param name="card">The card you want to authenticate against.</param>
        /// <param name="block">The block to authenticate against. Must be a trailer block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <remarks>You must authenticate against the trailer block for every sector you wish to perform operations on.</remarks>
        /// <seealso cref="SetKeyA(byte[])"/>
        public ReaderResponseStatus AuthenticateKeyA(ReaderCard card, byte block)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("AuthenticateKeyA is only for Mifare cards");
            
            return AuthenticateKeyA(card.uid, block);
        }

        /// <summary>
        /// Select a card and authenticate against it using the reader's Key B.
        /// Used to gain permission to perform operations (read/write) on the blocks in a card sector.
        /// </summary>
        /// <param name="uid">The UID of the card you want to authenticate against.</param>
        /// <param name="block">The block to authenticate against. Must be a trailer block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>You must authenticate against the trailer block for every sector you wish to perform operations on.</remarks>
        /// <seealso cref="SetKeyB(byte[])"/>
        public ReaderResponseStatus AuthenticateKeyB(byte[] uid, byte block)
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            var payload = new byte[5];
            Array.Copy(uid, 0, payload, 0, 4);
            payload[4] = block;
            
            SendCommand(new ReaderCommand(ReaderCommandType.MifareAuthKeyB, payload));
            var resp = GetResponse();
            
            return resp.status;
        }
        
        /// <summary>
        /// Select a card and authenticate against it using the reader's Key B.
        /// Used to gain permission to perform operations (read/write) on the blocks in a card sector.
        /// </summary>
        /// <param name="card">The card you want to authenticate against.</param>
        /// <param name="block">The block to authenticate against. Must be a trailer block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <remarks>You must authenticate against the trailer block for every sector you wish to perform operations on.</remarks>
        /// <seealso cref="SetKeyB(byte[])"/>
        public ReaderResponseStatus AuthenticateKeyB(ReaderCard card, byte block)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("AuthenticateKeyB is only for Mifare cards");
            
            return AuthenticateKeyB(card.uid, block);
        }

        /// <summary>
        /// Reads a block from the selected card. You must be authenticated with the sector the block is in,
        /// and have permission to read the block according to the access bits of the sector.
        /// </summary>
        /// <param name="uid">The UID of the card you want to read from.</param>
        /// <param name="block">The block you want to read from.</param>
        /// <returns>The contents of the block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <remarks>You must have a card selected with <see cref="SelectCard(byte[])"/> before using this.</remarks>
        /// <seealso cref="AuthenticateKeyA(byte[],byte)"/>
        /// <seealso cref="AuthenticateKeyB(byte[],byte)"/>
        public byte[] ReadBlock(byte[] uid, byte block)
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            var payload = new byte[5];
            Array.Copy(uid, 0, payload, 0, 4);
            payload[4] = block;
            
            SendCommand(new ReaderCommand(ReaderCommandType.MifareReadBlock, payload));
            var resp = GetResponse();
            
            if(resp.status != ReaderResponseStatus.Ok)
                throw new Exception($"ReadBlock (block {block}) failed with status {resp.status}");
            
            return resp.payload;
        }
        
        /// <summary>
        /// Reads a block from the selected card. You must be authenticated with the sector the block is in,
        /// and have permission to read the block according to the access bits of the sector.
        /// </summary>
        /// <param name="card">The card you want to read from.</param>
        /// <param name="block">The block you want to read from.</param>
        /// <returns>The contents of the block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <remarks>You must have a card selected with <see cref="SelectCard(ReaderCard)"/> before using this.</remarks>
        /// <seealso cref="AuthenticateKeyA(ReaderCard,byte)"/>
        /// <seealso cref="AuthenticateKeyB(ReaderCard,byte)"/>
        public byte[] ReadBlock(ReaderCard card, byte block)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("ReadBlock is only for Mifare cards");
            
            return ReadBlock(card.uid, block);
        }

        /// <summary>
        /// Writes a block to the selected card. You must be authenticated with the sector the block is in,
        /// and have permission to write the block according to the access bits of the sector.
        /// You will probably never need to do this.
        /// </summary>
        /// <param name="uid">The UID of the card you want to write to.</param>
        /// <param name="block">The block you want to write to.</param>
        /// <param name="data">The data to write to the block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if the data to write is not exactly 16 bytes.</exception>
        public ReaderResponseStatus WriteBlock(byte[] uid, byte block, byte[] data)
        {
            if (!radioEnabled) throw new InvalidOperationException("Reader radio is not enabled");
            
            if(data.Length != 16) throw new ArgumentException("Invalid block data length");
            
            var payload = new byte[5];
            Array.Copy(uid, 0, payload, 0, 4);
            payload[4] = block;
            SendCommand(new ReaderCommand(ReaderCommandType.MifareAuthKeyB, payload));
            var resp = GetResponse();
            
            return resp.status;
        }
        
        /// <summary>
        /// Writes a block to the selected card. You must be authenticated with the sector the block is in,
        /// and have permission to write the block according to the access bits of the sector.
        /// You will probably never need to do this.
        /// </summary>
        /// <param name="card">The card you want to write to.</param>
        /// <param name="block">The block you want to write to.</param>
        /// <param name="data">The data to write to the block.</param>
        /// <returns>The response status from the reader.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <exception cref="ArgumentException">Thrown if the data to write is not exactly 16 bytes, or if <paramref name="card"/> is not a Mifare card.</exception>
        public ReaderResponseStatus WriteBlock(ReaderCard card, byte block, byte[] data)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("ReadBlock is only for Mifare cards");
            
            return WriteBlock(card.uid, block, data);
        }

        /// <summary>
        /// Sets the color intensity (value 0-255) of the specified channel(s) on the reader LEDs.
        /// </summary>
        /// <param name="channel">The color channel(s) to change the intensity of. Flag enum.</param>
        /// <param name="value">The intensity to set the selected color channel(s) to.</param>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        /// <remarks>Current color can be queried with <see cref="readerColor"/>.</remarks>
        public void SetColorIntensity(ReaderColorChannel channel, byte value)
        {
            SendCommand(new ReaderCommand(ReaderCommandType.LightSetChannel, new []{ (byte)channel, value }));
            #if UNITY
            _readerColor = new Color32(
            #else
            _readerColor = new LightColor(
            #endif
                channel.HasFlag(ReaderColorChannel.Red) ? value : readerColor.r,
                channel.HasFlag(ReaderColorChannel.Green) ? value : readerColor.g,
                channel.HasFlag(ReaderColorChannel.Blue) ? value : readerColor.b,
                0xFF);
        }
        
        /// <summary>
        /// Sets the color of the reader LEDs.
        /// </summary>
        /// <param name="color">The color to set the reader to.</param>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        /// <remarks>Current color can be queried with <see cref="readerColor"/>.</remarks>
        #if UNITY
        public void SetColor(Color32 color)
        #else
        public void SetColor(LightColor color)
        #endif
        {
            SendCommand( new ReaderCommand(ReaderCommandType.LightSetColor, new []{ (byte)color.r, (byte)color.g, (byte)color.b } ));
            _readerColor = color;
        }

        /// <summary>
        /// Sets the color of the reader LEDs.
        /// </summary>
        /// <param name="r">The red value of the color.</param>
        /// <param name="g">The green value of the color.</param>
        /// <param name="b">The blue value of the color.</param>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        /// <remarks>Current color can be queried with <see cref="readerColor"/>.</remarks>
        public void SetColor(byte r, byte g, byte b) 
        {
            #if UNITY
            SetColor(new Color32(r,g,b,0xFF));
            #else
            SetColor(new LightColor(r,g,b));
            #endif
        }
        
        /// <summary>
        /// Asks the reader for the firmware version it's running.
        /// Stores it in <see cref="firmwareVersion"/>. 
        /// </summary>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        private void GetFirmwareVersion()
        {
            SendCommand(new ReaderCommand(ReaderCommandType.GetFirmwareVersion));
            var resp = GetResponse();
            
            if(resp.status != ReaderResponseStatus.Ok)
                throw new Exception($"GetFirmwareVersion failed with status {resp.status}");
            
            firmwareVersion = resp.payload[0];

            if (firmwareVersion != 0x94)
            {
                #if UNITY
                Debug.LogWarning("[LilyConsole] Reader firmware version not recognized, hoping for the best...");
                #else
                if (DebugMode) Console.WriteLine("Warning: Reader firmware version not recognized, hoping for the best...");
                #endif
            }
        }

        /// <summary>
        /// Asks the reader for the hardware version it's running.
        /// Stores it in <see cref="hardwareVersion"/>. 
        /// </summary>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        private void GetHardwareVersion()
        {
            SendCommand(new ReaderCommand(ReaderCommandType.GetHardwareVersion));
            var resp = GetResponse();
            
            if(resp.status != ReaderResponseStatus.Ok) 
                throw new Exception($"GetHardwareVersion failed with status {resp.status}");
            
            hardwareVersion = Encoding.ASCII.GetString(resp.payload);
        }
        
        /// <summary>
        /// Asks the reader to reset, performing the magic handshake. This can only be called once per power cycle,
        /// or else it will respond with <see cref="ReaderResponseStatus.InvalidCommand"/>,
        /// which can be safely ignored, so we do so.
        /// </summary>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <remarks>
        /// Call this before sending any other commands to the reader.
        /// If you do not, it will not respond to anything.
        /// </remarks>
        private void Reset()
        {
            SendCommand(new ReaderCommand(ReaderCommandType.Reset));
            var resp = GetResponse();

            if (resp.status != ReaderResponseStatus.Ok && resp.status != ReaderResponseStatus.InvalidCommand)
                throw new Exception($"Reset failed with status {resp.status}");

            ready = true;
        }
        
        #endregion
        
        #region Helper Functions
        
        /// <summary>
        /// Reads the access code from a card identified by the UID.
        /// </summary>
        /// <param name="uid">The UID of the card you want read the access code from.</param>
        /// <returns>The 10-byte access code.</returns>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>You can store the return value of this method in the <see cref="ReaderCard.accessCode"/> property of the <paramref name="card"/>.</remarks>
        public byte[] ReadAccessCode(byte[] uid)
        {
            var accessCode = new byte[10];
            
            var status = AuthenticateKeyA(uid, 3);
            if(status != ReaderResponseStatus.Ok) 
                throw new Exception($"ReadAccessCode (AuthenticateKeyA) failed with status {status}");

            var block = ReadBlock(uid, 2);
            Array.Copy(block, 6, accessCode, 0, accessCode.Length);
            
            return accessCode;
        }
        
        /// <summary>
        /// Reads the access code from a card.
        /// </summary>
        /// <param name="card">The card you want read the access code from.</param>
        /// <returns>The 10-byte access code.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>You can store the return value of this method in the <see cref="ReaderCard.accessCode"/> property of the <paramref name="card"/>.</remarks>
        public byte[] ReadAccessCode(ReaderCard card)
        {
            if (card.type != ReaderCardType.Mifare) throw new ArgumentException("ReadAccessCode is only for Mifare cards");
            
            return ReadAccessCode(card.uid);
        }
        
        /// <summary>
        /// Reads the access code from the last detected card. You probably shouldn't use this.
        /// </summary>>
        /// <returns>The 10-byte access code.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is not a Mifare card.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>You can store the return value of this method in the <see cref="ReaderCard.accessCode"/> property of the <paramref name="card"/>.</remarks>
        public byte[] ReadAccessCode()
        {
            return ReadAccessCode(_lastPoll[0]);
        }

        /// <summary>
        /// Fills out additional fields of a <see cref="ReaderCard"/> struct by asking the card.
        /// </summary>
        /// <param name="card">The card to fill info from.</param>
        /// <returns>The modified card information.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is a FeliCa card but has invalid data.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        /// <remarks>This doesn't do anything useful for FeliCa cards yet.</remarks>
        public ReaderCard ReadCardInfo(ReaderCard card)
        {
            
            if (card.type == ReaderCardType.FeliCa)
            {
                // we already have as much info about the card as we can for now, just return it.
                if (card.idm.Length == 8 && card.pmm.Length == 8) return card;

                throw new ArgumentException("Invalid FeliCa card");
            }
            
            card.accessCode = ReadAccessCode(card);
            return card;
        }

        /// <summary>
        /// Fills out additional fields of the last detected card by asking the card.
        /// If you just want the card data after polling without having to mess with it,
        /// this is the method you want to call.
        /// </summary>
        /// <returns>The modified card information.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="card"/> is a FeliCa card but has invalid data.</exception>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized, or if the radio is not enabled.</exception>
        public ReaderCard ReadCardInfo()
        {
            return ReadCardInfo(_lastPoll[0]);
        }
        
        /// <summary>
        /// Turns the reader LEDs off by setting the color to black.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>

        public void ClearColor()
        {
            SetColor(0,0,0);
        }
        
        /// <summary>
        /// Initializes the keys used by the type of cards you'll probably be reading.
        /// </summary>
        /// <exception cref="Exception">Thrown if the reader responds with an error.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader is not initialized.</exception>
        public void SetDefaultKeys()
        {
            var resp = SetKeyA(new byte[]{0x60,0x90,0xD0,0x06,0x32,0xF5});
            if(resp != ReaderResponseStatus.Ok) throw new Exception($"SetDefaultKeys (Key A) failed with status {resp}");
            
            resp = SetKeyB(new byte[]{0x57,0x43,0x43,0x46,0x76,0x32});
            if(resp != ReaderResponseStatus.Ok) throw new Exception($"SetDefaultKeys (Key B) failed with status {resp}");
        }
        
        #endregion
        
        #region Low-Level IO
        
        private void SendData(byte[] data)
        {
            if (DebugMode)
            {
                #if UNITY
                Debug.Log("[LilyConsole] READER -> " + BitConverter.ToString(data));
                #else
                Console.WriteLine("-> " + BitConverter.ToString(data));
                #endif
            }
             
            port.Write(data, 0, data.Length);
        }

        private void SendCommand(ReaderCommand cmd)
        {
            if(!ready && cmd.command != ReaderCommandType.Reset)
                throw new InvalidOperationException("Reader not initialized");
            
            var raw = new byte[cmd.payload.Length + 7];
                
            raw[1] = (byte)(raw.Length - 2); // exclusive length, minus marker
            raw[2] = 0;
            raw[3] = currentSeq;
            raw[4] = (byte)cmd.command;
            raw[5] = (byte)cmd.payload.Length; // inclusive
            if (raw[5] != 0) Array.Copy(cmd.payload, 0, raw, 6, cmd.payload.Length);
            raw[raw.Length - 1] = ReaderCommand.MakeChecksum(raw);
            raw = ReaderCommand.EscapeBytes(raw);
            raw[0] = 0xe0;
            
            SendData(raw);
            unchecked{ currentSeq++; }
        }

        /// <summary>
        /// Retrieves a response from the reader.
        /// </summary>
        /// <remarks>This does not validate the checksum of the received response, I'm lazy</remarks>
        /// <returns>The response from the reader</returns>
        /// <exception cref="Exception">Thrown when the marker byte is invalid</exception>
        private ReaderResponse GetResponse()
        {
            var marker = (byte)port.ReadByte();
            if (marker != 0xe0) throw new Exception($"Invalid response (read {marker:X2}, expected E0)");
            
            var len = (byte)port.ReadByte();
            var final = new byte[len + 2];
            
            final[0] = marker;
            final[1] = len;

            // has to be unescaped twice because funny, read and unescape
            var buf = new List<byte>();
            for (var i = 0; i < len; i++)
            {
                var b = (byte)port.ReadByte();
                if (b == 0xd0) b = (byte)(port.ReadByte() + 1);
                buf.Add(b);
            }
            
            var data = ReaderCommand.UnescapeBytes(buf.ToArray());
            Array.Copy(data, 0, final, 2, data.Length);
            
            if (DebugMode)
            {
                #if UNITY
                Debug.Log("[LilyConsole] READER <- " + BitConverter.ToString(final));
                #else
                Console.WriteLine("<- " + BitConverter.ToString(final));
                #endif
            }
            
            return new ReaderResponse(final);
        }
        
        #endregion
    }
}