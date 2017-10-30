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
        static LogWindow s_lw;

        static void Main(string[] args)
        {
            BtcNetwork.Initialize();

            s_lw = new LogWindow();
            Logger log = new Logger();
            log.OnLogString += Log_OnLogString;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Connections: " + BtcNetwork.NumConnections);


                Thread.Sleep(200);
            }
        }

        private static void Log_OnLogString(string obj)
        {
            s_lw?.WriteLine(obj);
        }
    }
}
