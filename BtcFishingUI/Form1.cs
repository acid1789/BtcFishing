using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BtcFishingUI
{
    public partial class Form1 : Form
    {
        UILogger _logger;

        public Form1()
        {
            InitializeComponent();

            _logger = new UILogger();
            _logger.OnLogString += _logger_OnLogString;

            BtcLib.BtcBlockChain.Initialize("D:/Bitcoin");
            BtcLib.BtcNetwork.OnlyBlockSendingNodes = true;
            BtcLib.BtcNetwork.Initialize();
        }

        private void _logger_OnLogString(string obj)
        {
            if (InvokeRequired)
                Invoke((MethodInvoker)delegate { _logger_OnLogString(obj); });
            else
                tbOutput.AppendText(obj + "\r\n");
            
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                lblConnections.Text = BtcLib.BtcNetwork.NumConnections.ToString();
                lblPotentials.Text = BtcLib.BtcNetwork.NumPotentialConnections.ToString();
                lblBlockHeaders.Text = string.Format("Banned {0} / Not {1}", BtcLib.BtcBlockChain.BannedCount, BtcLib.BtcNetwork.NumConnections - BtcLib.BtcBlockChain.BannedCount);
                lblBlocks.Text = string.Format("Blocks {0} / {1}", BtcLib.BtcBlockChain.CachedBlocks, BtcLib.BtcBlockChain.KnownHeight);
            }
            catch (Exception) { }
        }
    }
}
