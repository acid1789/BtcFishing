using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using BtcLib;

namespace BtcFishing
{
    class Program
    {
        static void Main(string[] args)
        {
            BtcNetwork.Initialize();

            while (true)
            {
                Thread.Sleep(200);
            }
        }
    }
}
