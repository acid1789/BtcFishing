using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BtcLib
{
    public class BtcBlockRequest
    {
        public DateTime LastSeenTime;
        public int RequestedCount;
        public HashSet<string> RequestedBlocks;
    }
}
