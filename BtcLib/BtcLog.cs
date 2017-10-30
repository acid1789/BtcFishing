using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BtcLib
{
    public abstract class BtcLog
    {
        static BtcLog s_Instance;
        protected BtcLog()
        {
            s_Instance = this;
        }

        protected abstract void LogString(string str);

        public static void Print(string fmt, params string[] args)
        {
            s_Instance?.LogString(string.Format(fmt, args));
        }
    }
}
