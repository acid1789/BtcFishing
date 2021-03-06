﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;

namespace BtcLib
{
    public class BtcUtils
    {
        public static byte[] DSha256(byte[] input)
        {
            SHA256 sha256 = SHA256Managed.Create();            
            byte[] hash = sha256.ComputeHash(sha256.ComputeHash(input));
            return hash;
        }

        public static bool HashEquals(byte[] hashA, byte[] hashB)
        {
            if (hashA.Length != hashB.Length)
                return false;

            for (int i = 0; i < hashA.Length; i++)
            {
                if (hashA[i] != hashB[i])
                    return false;
            }
            return true;
        }

        public static ulong UnixTime { get { return (ulong)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds; } }

        public static void PrintBytes(byte[] data, int length = -1)
        {
            if (length <= 0)
                length = data.Length;

            int rows = length / 16;
            if (length % 16 != 0)
                rows++;

            int index = 0;
            for (int i = 0; i < rows; i++)
            {
                int lineIndex = index;
                for (int j = 0; j < 16; j++)
                {
                    if (index < length)
                        Console.Write("{0:X2} ", data[index++]);
                    else
                        Console.Write("   ");
                    if (j == 7)
                        Console.Write(" ");
                }

                Console.Write(" ");
                for (int j = 0; j < 16 && lineIndex < length; j++)
                {
                    byte c = data[lineIndex++];
                    char cc = (c >= 'a' && c <= 'z' ||
                                c >= 'A' && c <= 'Z' ||
                                c >= '0' && c <= '9') ? (char)c : '.';
                    Console.Write(cc);
                }

                Console.WriteLine("");
            }
        }

        public static void WriteVarInt(BinaryWriter bw, long val)
        {
            if (val < 0xFD)
                bw.Write((byte)val);
            else if (val < 0xFFFF)
            {
                bw.Write((byte)0xFD);
                bw.Write((ushort)val);
            }
            else if (val < 0xFFFFFFFF)
            {
                bw.Write((byte)0xFE);
                bw.Write((uint)val);
            }
            else
            {
                bw.Write((byte)0xFF);
                bw.Write(val);
            }
        }

        public static long ReadVarInt(BinaryReader br)
        {
            byte b = br.ReadByte();
            switch (b)
            {
                case 0xFD: return (long)br.ReadInt16();
                case 0xFE: return (long)br.ReadInt32();
                case 0xFF: return (long)br.ReadInt64();
                default: return (long)b;
            }
        }

        public static string ReadVarString(BinaryReader br)
        {
            long len = ReadVarInt(br);
            byte[] bytes = br.ReadBytes((int)len);
            return Encoding.ASCII.GetString(bytes);
        }

        public static int[] ReadIntSet(BinaryReader br)
        {
            long count = ReadVarInt(br);
            int[] set = new int[count];
            for (long i = 0; i < count; i++)
            {
                set[i] = br.ReadInt32();
            }
            return set;
        }

        public static string[] ReadStringSet(BinaryReader br)
        {
            long count = ReadVarInt(br);
            string[] set = new string[count];
            for (long i = 0; i < count; i++)
            {
                set[i] = ReadVarString(br);
            }
            return set;
        }

        public static string BytesToString(byte[] bytes, bool prefix = false)
        {
            string str = prefix ? "0x" : "";
            foreach (byte b in bytes)
                str += b.ToString("X2");
            return str;
        }

        public static byte[] StringToBytes(string str)
        {
            byte[] bytes = new byte[str.Length / 2];
            for (int i = 0; i < bytes.Length; i++ )
            {
                string s = str.Substring(i * 2, 2);
                bytes[i] = byte.Parse(s, System.Globalization.NumberStyles.AllowHexSpecifier);
            }
            return bytes;
        }
    }
}
