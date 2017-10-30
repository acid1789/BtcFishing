using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BtcLib
{
    public class BtcBlockChain
    {
        #region Public Static Interface
        public static uint Height { get { return 0; } }

        public static void AddKnownBlock(byte[] blockHash)
        {
            BtcLog.Print("AddKnownBlock: " + BtcUtils.BytesToString(blockHash));
        }
        #endregion

        #region Private Interface
        #endregion
    }
}
