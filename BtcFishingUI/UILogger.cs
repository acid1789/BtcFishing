using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BtcLib;

namespace BtcFishingUI
{
    class UILogger : BtcLog
    {
        public event Action<string> OnLogString;

        public UILogger()
        {
        }

        protected override void LogString(string str)
        {
            OnLogString?.Invoke(str);
        }
    }
}
