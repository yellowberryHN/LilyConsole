using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace LilyConsole.Helpers
{
    // https://eamuse.bsnk.me/cardid.html#eaid
    // more or less a direct port of the logic described there. might suck.
    public static class AmuseIC
    {
        private static readonly char[] Alphabet = "0123456789ABCDEFGHJKLMNPRSTUWXYZ".ToCharArray();
        private static readonly byte[] Key =
        {
            0x7E,0x92,0x4E,0xD8,0xD8,0x84,0x64,0xC6,0x5C,0xB2,0xDE,0xEA,
            0xB0,0xB0,0xB0,0xCA,0x9A,0xCA,0x90,0xC2,0xB2,0xE0,0xF2,0x42
        };

        public static string GetID(byte[] idm)
        {
            if((idm[0] >> 4 & 0xF) != 0) throw new ArgumentException("Not an AIC card");
            
            var data = idm;
            Array.Reverse(data);

            data = Unpack(Encrypt(data));
            
            Array.Resize(ref data, 16);
            data[0] ^= 2; // card type will always be felica, it's an NFC reader bro
            data[13] = 1;
            
            for (var i = 0; i <= 13; i++) data[i + 1] ^= data[i];
            
            data[14] = 2;
            data[15] = Checksum(data);

            // map transformed data into ID
            var id = new StringBuilder(16);
            for (var i = 0; i < 16; i++) id.Append(Alphabet[data[i]]);

            return id.ToString();
        }

        private static byte[] Encrypt(byte[] data)
        {
            using (var tdes = TripleDES.Create())
            {
                tdes.Key = Key;
                tdes.Mode = CipherMode.CBC;
                tdes.Padding = PaddingMode.None;
                tdes.IV = new byte[8];

                using (var encryptor = tdes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }
        
        private static byte[] Unpack(byte[] data)
        {
            var binaryString = new StringBuilder();
            foreach (var b in data)
            {
                binaryString.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            }
        
            var padding = (5 - binaryString.Length % 5) % 5;
            binaryString.Append('0', padding);

            var unpackedBytes = new List<byte>();
            for (var i = 0; i < binaryString.Length; i += 5)
            {
                unpackedBytes.Add(Convert.ToByte(binaryString.ToString(i, 5), 2));
            }

            if (unpackedBytes.Count > 13)
            {
                unpackedBytes.RemoveRange(13, unpackedBytes.Count - 13);
            }

            return unpackedBytes.ToArray();
        }

        private static byte Checksum(byte[] data)
        {
            var chk = 0;
            
            for (var i = 0; i <= 14; i++) chk += data[i] * (i % 3 + 1);
            while (chk > 31) chk = (chk >> 5) + (chk & 31);

            return (byte)chk;
        }
    }
}