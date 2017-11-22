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
        public static int NumConnections { get { return (s_Instance == null) ? 0 : s_Instance._Connections.Count; } }
        public static int NumPotentialConnections { get { return s_Instance._PossiblePeers.Count; } }
        public static int HighestHeight { get { return s_Instance._highestHeight; } }
        public static bool HeaderFetchEnabled { get; set; }

        public static BtcSocket[] CurrentConnections
        {
            get
            {
                s_Instance._ConnectionsLock.WaitOne();
                BtcSocket[] list = s_Instance._Connections.ToArray();
                s_Instance._ConnectionsLock.ReleaseMutex();

                return list;
            }
        }
        #endregion


        #region Private Interface

        #region Constants
        const int NumSpiderThreads = 25;
        const int MaxConnections = 1000;
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
        Thread[] _SpiderThreads;
        Thread _NetworkThread;
        Dictionary<string, bool> _BadHosts;
        Queue<string> _PossiblePeers;
        Mutex _PossiblePeerLock;

        Mutex _ConnectionsLock;
        List<BtcSocket> _Connections;

        int _highestHeight;
        BtcSocket _highestHost;
        DateTime _headersRequestedTime;
        bool _waitingForHeaders;

        BtcNetwork()
        {
            HeaderFetchEnabled = true;
            _highestHeight = 0;
            _BadHosts = new Dictionary<string, bool>();

            Random r = new Random();
            _BitcoinNodeId = ((ulong)r.Next() << 32) | ((ulong)r.Next());
            _ConnectionsLock = new Mutex();
            _Connections = new List<BtcSocket>();
            _PossiblePeers = new Queue<string>(BitcoinSeeds);
            _PossiblePeerLock = new Mutex();

            _SpiderThreads = new Thread[NumSpiderThreads];
            for (int i = 0; i < _SpiderThreads.Length; i++)
            {
                _SpiderThreads[i] = new Thread(new ThreadStart(SpiderThreadProcedure)) { Name = "SpiderThread " + i };
                _SpiderThreads[i].Start();
            }

            _NetworkThread = new Thread(new ThreadStart(NetworkThreadProcedure)) { Name = "Network Thread" };
            _NetworkThread.Start();
        }

        void SpiderThreadProcedure()
        {
            while (true)
            {
                // Try to connect to any possible peers
                if (_PossiblePeers.Count > 0 && _Connections.Count < MaxConnections)
                {
                    string peer = null;
                    _PossiblePeerLock.WaitOne();
                    if( _PossiblePeers.Count > 0 )
                        peer = _PossiblePeers.Dequeue();
                    _PossiblePeerLock.ReleaseMutex();
                    if (peer == null || _BadHosts.ContainsKey(peer))
                        continue;
                    BtcSocket socket = new BtcSocket();
                    if (socket.Connect(peer, BitcoinPort))
                    {
                        socket.OnNodeDiscovered += Socket_OnNodeDiscovered;
                        socket.OnInventory += Socket_OnInventory;
                        socket.OnHeader += Socket_OnHeader;
                        socket.OnBlock += Socket_OnBlock;
                        _ConnectionsLock.WaitOne();
                        _Connections.Add(socket);
                        _ConnectionsLock.ReleaseMutex();
                    }
                    else
                        _BadHosts[peer] = true;
                }

                Thread.Sleep(50);
            }
        }

        void NetworkThreadProcedure()
        {
            while (true)
            {
                // Update all existing connections
                _ConnectionsLock.WaitOne();
                List<BtcSocket> remove = new List<BtcSocket>();
                foreach (BtcSocket peer in _Connections)
                {
                    if (!peer.Update())
                        remove.Add(peer);
                    else if (peer.RemoteBlockHeight >= _highestHeight)
                    {
                        if (_highestHost == null || peer.Score > _highestHost.Score)
                        {
                            _highestHeight = peer.RemoteBlockHeight;
                            _highestHost = peer;
                        }
                    }
                }

                // Remove any dead connections
                foreach (BtcSocket r in remove)
                    _Connections.Remove(r);
                _ConnectionsLock.ReleaseMutex();

                if (HeaderFetchEnabled)
                    UpdateHeaders();
            }
        }

        void UpdateHeaders()
        {
            TimeSpan ts = DateTime.Now - _headersRequestedTime;
            if (ts.TotalSeconds < 3)
            {
                // Requested headers less than 15 seconds ago, wait
            }
            else
            {
                // Last header request was a while ago
                if (_waitingForHeaders)
                {
                    BtcLog.Print("Timed out waiting for headers from: " + _highestHost.RemoteHost);
                    _waitingForHeaders = false;
                    _highestHost.DecrementScore();
                }
                else if (BtcBlockChain.KnownHeight < _highestHeight)
                {
                    // There are still headers to fetch
                    BtcLog.Print("Requesting headers from: " + _highestHost.RemoteHost);
                    _highestHost.SendGetHeadersPacket();
                    _headersRequestedTime = DateTime.Now;
                    _waitingForHeaders = true;
                }
            }
        }

        private void Socket_OnHeader(BtcSocket arg1, BtcBlockHeader arg2)
        {
            BtcBlockChain.AddBlockHeader(arg2);
        }
        
        private void Socket_OnBlock(BtcSocket arg1, BtcBlockHeader arg2, BtcTransaction[] arg3)
        {
            BtcBlockChain.IncommingBlock(arg1, arg2, arg3);
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
            string incommingIp = arg2.ToString();
            if (_BadHosts.ContainsKey(incommingIp))
                return;

            // Check to see if this address is already in our connections list
            foreach (BtcSocket s in _Connections)
            {
                if (s.IsAddress(arg2))
                    return; // Already connected to this peer
            }

            // Check to see if this is in the potentials list            
            _PossiblePeerLock.WaitOne();
            foreach (string potential in _PossiblePeers)
            {
                if (potential == incommingIp)
                {
                    _PossiblePeerLock.ReleaseMutex();
                    return; // Already in the list to connect to
                }
            }

            // Still here? Add this to the potential queue
            _PossiblePeers.Enqueue(incommingIp);
            _PossiblePeerLock.ReleaseMutex();
        }

        void InternalFetchHeaders()
        {
            //_Connections[0].FetchHeaders();
        }
        #endregion

    }
}


