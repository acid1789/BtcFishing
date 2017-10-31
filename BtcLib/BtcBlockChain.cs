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

        public static void AddBlockHeader(BtcBlockHeader header)
        {
            s_Instance.AddHeader(header);
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

        List<BtcBlockHeader> _chainFragments;
        BtcBlockHeader _mainChain;

        BtcBlockChain()
        {
            _chainFragments = new List<BtcBlockHeader>();
            _mainChain = null;

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
        }

        void UpdateHeaders()
        {
        }

        void UpdateBlocks()
        {
        }

        void AddHeader(BtcBlockHeader header)
        {
            if (_mainChain == null && BtcUtils.HashEquals(header.GetHash(), BtcSocket.OriginBlockHash))
            {
                // Found the origin block
                _mainChain = header;
            }

            // Link any other blocks we can
            LinkBlocksTo(header);
        }

        void LinkBlocksTo(BtcBlockHeader block)
        {
            List<BtcBlockHeader> remove = new List<BtcBlockHeader>();
            foreach (BtcBlockHeader fragment in _chainFragments)
            {
                if (BtcUtils.HashEquals(fragment.GetHash(), block.PrevHash))
                {
                    if (fragment.Next != null)
                    {
                        // Fragment already has a next block, but this new block is pointing to it as previous
                        if (BtcUtils.HashEquals(fragment.Next.GetHash(), block.GetHash()))
                        {
                            // The new block is a duplicate of the existing block, just bail
                            return;
                        }
                        else
                        {
                            // Both blocks point here, this is a problem!
                            BtcLog.Print("Multiple blocks pointing to one parent block!");
                        }

                    }
                    else
                    {
                        fragment.Next = block;
                        block.Prev = fragment;
                    }
                }
                else if (BtcUtils.HashEquals(block.GetHash(), fragment.PrevHash))
                {
                    block.Next = fragment;
                    fragment.Prev = block;
                    remove.Add(fragment);
                }
            }

            foreach (BtcBlockHeader r in remove)
                _chainFragments.Remove(r);
        }
        #endregion
    }
}
