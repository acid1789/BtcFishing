using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BtcLib
{
    public class BtcUtils
    {
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
    }
}
