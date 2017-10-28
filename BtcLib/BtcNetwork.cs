using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

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

        public static ulong BitcionNodeId { get { return s_Instance._BitcoinNodeId; } }
        #endregion


        #region Private Interface

        #region Constants
        const int BitcoinPort = 8333;
        string[] BitcoinSeeds = { /*"seed.bitcoin.sipa.be",*/
                                    "dnsseed.bluematt.me",
                                    "dnsseed.bitcoin.dashjr.org",
                                    "seed.bitcoinstats.com",
                                    "seed.bitcoin.jonasschnelli.ch",
                                    "seed.btc.petertodd.org" };
        #endregion

        ulong _BitcoinNodeId;
        Thread _SpiderThread;
        Queue<string> _PossiblePeers;
        List<BtcSocket> _Connections;

        BtcNetwork()
        {
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
                    _Connections.Add(socket);                    
                }
            }
        }

        private void Socket_OnNodeDiscovered(BtcSocket arg1, BtcNetworkAddress arg2)
        {
            // Check to see if this address is already in our connections list
            foreach (BtcSocket s in _Connections)
            {
                if (s == arg2)
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
        #endregion

    }
}
