using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BtcFishing
{
    class LogWindow
    {
        Process _p;
        StreamWriter _stdIn;

        public LogWindow()
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardError = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            _p = Process.Start(psi);            
            _stdIn = _p.StandardInput;
        }

        public void WriteLine(string str)
        {
            _stdIn.WriteLine(str);
        }
    }
}
