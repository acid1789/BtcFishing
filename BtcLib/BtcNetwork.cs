using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace BtcLib
{
    public class BtcNetwork
    {

        #region Public Static Interface
        static BtcNetwork s_Instance;

        public static void Initialize()
        {
            if (s_Instance != null)
                throw new Exception("BtcLib.BtcNetwork is already initialized");

            s_Instance = new BtcNetwork();

        }

        public static void Shutdown()
        {
            if (s_Instance != null)
            {
                s_Instance = null;
            }
        }

        public static void FetchHeaders()
        {
            s_Instance.InternalFetchHeaders();
        }

        public static ulong BitcionNodeId { get { return s_Instance._BitcoinNodeId; } }
        public static int NumConnections { get { return s_Instance._Connections.Count; } }
        public static int HigestHeight { get { return s_Instance._heighestHeight; } }
        #endregion


        #region Private Interface

        #region Constants
        const int BitcoinPort = 8333;
        string[] BitcoinSeeds = { "66.172.10.4",
                                    "64.203.102.86",
                                    "67.221.193.55",
                                    "66.194.38.250",
                                    "66.194.38.253",
                                    "62.210.66.227",
                                    "46.38.235.229",
                                    "46.166.161.103",
                                    "46.227.66.132",
                                    "80.240.129.221",
                                    "81.80.9.71",
                                    "83.162.196.192",
                                    "82.199.102.10",
                                    "84.245.71.31",
                                    "84.47.161.150",
                                    "82.200.205.30",
                                    "91.106.194.97",
                                    "92.27.7.209",
                                    "104.128.228.252",
                                    "104.238.130.182",
                                    "154.20.2.139",
                                    "178.254.34.161",
                                    "185.28.76.179",
                                    "192.203.228.71",
                                    "193.234.225.156",
                                    "202.55.87.45",
                                    "211.72.66.229",
                                    "213.184.8.22",
                                    "seed.bitcoinstats.com",
                                    "seed.bitcoin.sipa.be",
                                    "dnsseed.bluematt.me",
                                    "dnsseed.bitcoin.dashjr.org",
                                    "seed.bitcoin.jonasschnelli.ch",
                                    "bitseed.xf2.org",
                                    "79.132.230.144",
                                    "seed.btc.petertodd.org" };

        public enum InventoryType
        {
            Error,
            Transaction,
            Block,
            FilteredBlock,
            CmpctBlock
        }


        #endregion

        ulong _BitcoinNodeId;
        Thread _SpiderThread;
        Queue<string> _PossiblePeers;
        List<BtcSocket> _Connections;

        int _heighestHeight;

        BtcNetwork()
        {
            _heighestHeight = 0;

            Random r = new Random();
            _BitcoinNodeId = ((ulong)r.Next() << 32) | ((ulong)r.Next());
            _Connections = new List<BtcSocket>();
            _PossiblePeers = new Queue<string>(BitcoinSeeds);
            _SpiderThread = new Thread(new ThreadStart(SpiderThreadProcedure)) { Name = "SpiderThread" };
            _SpiderThread.Start();

        }

        void SpiderThreadProcedure()
        {
            while (true)
            {
                // Try to connect to any possible peers
                ConnectToPeer();

                // Update all existing connections
                List<BtcSocket> remove = new List<BtcSocket>();
                foreach (BtcSocket peer in _Connections)
                {
                    if (!peer.Update())
                        remove.Add(peer);
                    else if( peer.RemoteBlockHeight > _heighestHeight )
                        _heighestHeight = peer.RemoteBlockHeight;
                }

                // Remove any dead connections
                foreach (BtcSocket r in remove)
                    _Connections.Remove(r);


                Thread.Sleep(50);
            }
        }

        void ConnectToPeer()
        {
            if (_PossiblePeers.Count > 0)
            {
                string peer = _PossiblePeers.Dequeue();
                BtcSocket socket = new BtcSocket();
                if (socket.Connect(peer, BitcoinPort))
                {
                    socket.OnNodeDiscovered += Socket_OnNodeDiscovered;
                    socket.OnInventory += Socket_OnInventory;
                    _Connections.Add(socket);
                }
            }
        }

        void Socket_OnInventory(BtcSocket socket, InventoryType type, byte[] hash)
        {
            switch (type)
            {
                case InventoryType.Block: BtcBlockChain.AddKnownBlock(hash); break;
                case InventoryType.Transaction: break;
                default:
                    BtcLog.Print("Unhandled inventory type: " + type);
                    break;
            }
        }

        void Socket_OnNodeDiscovered(BtcSocket arg1, BtcNetworkAddress arg2)
        {
            // Check to see if this address is already in our connections list
            foreach (BtcSocket s in _Connections)
            {
                if (s.IsAddress(arg2))
                    return; // Already connected to this peer
            }

            // Check to see if this is in the potentials list
            foreach (string potential in _PossiblePeers)
            {
                if (potential == arg2.ToString())
                    return; // Already in the list to connect to
            }

            // Still here? Add this to the potential queue
            _PossiblePeers.Enqueue(arg2.ToString());
        }

        void InternalFetchHeaders()
        {
            //_Connections[0].FetchHeaders();
        }
        #endregion

    }
}


