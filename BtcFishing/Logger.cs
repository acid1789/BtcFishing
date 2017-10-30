using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BtcLib;

namespace BtcFishing
{
    public class Logger : BtcLog
    {
        List<string> _log;
        public event Action<string> OnLogString;

        public Logger() : base()
        {
            _log = new List<string>();
        }

        protected override void LogString(string str)
        {
            _log.Add(str);
            OnLogString?.Invoke(str);
        }

        public void PrintLog()
        {
            foreach (string s in _log)
            {
                Console.WriteLine(s);
            }
        }
    }
}
