using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BtcLib
{
    public class BtcBlockChain
    {
        #region Public Static Interface
        static BtcBlockChain s_Instance;
        public static uint Height { get { return s_Instance._height; } }
        public static uint KnownHeight { get { return s_Instance._knownHeight; } }

        public static void Initialize()
        {
            if (s_Instance != null)
                throw new Exception("BtcBlockChain singleton already initialized");

            s_Instance = new BtcBlockChain();
        }

        public static void Shutdown()
        {
            if (s_Instance != null)
            {
                if (s_Instance._thread != null)
                    s_Instance._thread.Abort();
                s_Instance._thread = null;
            }
        }

        public static void AddKnownBlock(byte[] blockHash)
        {
            BtcLog.Print("AddKnownBlock: " + BtcUtils.BytesToString(blockHash));
        }
        #endregion

        #region Private Interface
        enum BCState
        {
            Idle,
            FetchingHeaders,
            FetchingBlocks,
        }

        BCState _state;
        Thread _thread;

        uint _height;
        uint _knownHeight;

        BtcBlockChain()
        {
            _height = 0;
            _knownHeight = 0;

            _state = BCState.Idle;
            _thread = new Thread(new ThreadStart(BlockChainThreadProc)) { Name = "Block Chain Thread" };
            _thread.Start();
        }

        void BlockChainThreadProc()
        {
            while (true)
            {
                switch (_state)
                {
                    default:
                    case BCState.Idle: UpdateIdle(); break;
                    case BCState.FetchingHeaders: UpdateHeaders(); break;
                    case BCState.FetchingBlocks: UpdateBlocks(); break;
                }

                Thread.Sleep(100);
            }
        }

        void UpdateIdle()
        {
            try
            {
                if (_knownHeight < BtcNetwork.HigestHeight)
                {
                    _state = BCState.FetchingHeaders;
                    BtcNetwork.FetchHeaders();
                }
            }
            catch (Exception) { }
        }

        void UpdateHeaders()
        {
        }

        void UpdateBlocks()
        {
        }
        #endregion
    }
}
