using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.IO.Compression;

namespace BtcLib
{
    public class BtcBlockChain
    {
        #region Public Static Interface
        static BtcBlockChain s_Instance;
        public static uint Height { get { return s_Instance._height; } }
        public static int BannedCount { get { return s_Instance._bannedBlockHosts.Count; } }
        public static int CachedBlocks { get { return s_Instance._blockLibrary.Count; } }
        public static uint KnownHeight { get { return s_Instance._knownHeight; } }
        public static BtcBlockHeader Tip { get { return s_Instance._mainChain; } }

        public static void Initialize(string dataPath)
        {
            if (s_Instance != null)
                throw new Exception("BtcBlockChain singleton already initialized");

            Directory.CreateDirectory(dataPath);
            s_Instance = new BtcBlockChain(dataPath);
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
            BtcBlockHeader h = FindBlockHeader(blockHash);
            if (h == null)
                BtcLog.Print("AddKnownBlock: " + BtcUtils.BytesToString(blockHash));
        }

        public static void AddBlockHeader(BtcBlockHeader header)
        {
            s_Instance.AddHeader(header);
        }

        public static void IncommingBlock(BtcSocket from, BtcBlockHeader header, BtcTransaction[] transactions)
        {
            s_Instance.IncommingBlockI(from, header, transactions);
        }

        public static BtcBlockHeader FindBlockHeader(byte[] hash)
        {
            return s_Instance.FindHeader(hash);
        }

        public static void ReCount() { s_Instance.CountChain(); }
        #endregion

        #region Private Interface
        Thread _thread;
        string _dataPath;

        uint _height;
        uint _knownHeight;

        List<BtcBlockHeader> _chainFragments;
        BtcBlockHeader _mainChain;

        HashSet<string> _blockLibrary;

        DateTime _lastDiskSync;
        DateTime _lastBlockRequest;
        DateTime _lastBlockArchive;
        HashSet<BtcSocket> _bannedBlockHosts;
        Dictionary<BtcSocket, BtcBlockRequest> _pendingBlockRequests;
        Dictionary<string, object> _pendingIncommingBlocks;
        Mutex _pendingIncommingBlocksLock;

        Thread _blockArchiveThread;

        BtcBlockChain(string dataPath)
        {
            _dataPath = dataPath;
            _chainFragments = new List<BtcBlockHeader>();
            _mainChain = BtcBlockHeader.GenesisBlock;
            _height = 0;
            _knownHeight = 0;
            LoadHeaders();
            CountChain();

            InitBlockLibrary();
            _pendingBlockRequests = new Dictionary<BtcSocket, BtcBlockRequest>();
            _pendingIncommingBlocks = new Dictionary<string, object>();
            _pendingIncommingBlocksLock = new Mutex();
            _bannedBlockHosts = new HashSet<BtcSocket>();
            
            _thread = new Thread(new ThreadStart(BlockChainThreadProc)) { Name = "Block Chain Thread" };
            _thread.Start();
        }

        void BlockChainThreadProc()
        {
            while (true)
            {
                if ((DateTime.Now - _lastDiskSync).TotalSeconds > 10)
                    DoDiskSync();

                if (BtcNetwork.NumConnections > 10 && (DateTime.Now - _lastBlockRequest).TotalSeconds > 15 && _blockLibrary.Count < _knownHeight)
                    FetchMissingBlocks();

                ProcessPendingIncommingBlocks();

                if (_blockArchiveThread == null && (DateTime.Now - _lastBlockArchive).TotalSeconds > 300)
                {
                    _blockArchiveThread = new Thread(ArchiveBlocks);
                    _blockArchiveThread.Start();
                    _lastBlockArchive = DateTime.Now;
                }

                Thread.Sleep(100);
            }
        }

        void InitBlockLibrary()
        {
            // Make sure blocks directory exists
            Directory.CreateDirectory(_dataPath + "/blocks");

            // Get all blocks in the directory
            string prefix = _dataPath + "/blocks";
            string[] blocks = Directory.GetFiles(prefix, "*.block", SearchOption.TopDirectoryOnly);

            // Store them in the library
            _blockLibrary = new HashSet<string>();
            foreach (string block in blocks)
            {
                string bs = block.Substring(prefix.Length + 1, block.Length - (prefix.Length + 7));
                _blockLibrary.Add(bs);
            }

            // Index all the existing archives
            string[] archives = Directory.GetFiles(prefix, "*.archiveL", SearchOption.TopDirectoryOnly);
            foreach (string archive in archives)
            {
                string[] hashes = File.ReadAllLines(archive);
                foreach (string hash in hashes)
                    _blockLibrary.Add(hash);
            }
        }

        bool WaitingForBlock(string hashStr)
        {
            foreach (var kvp in _pendingBlockRequests)
            {
                if (kvp.Value.RequestedBlocks.Contains(hashStr))
                    return true;
            }
            return false;
        }

        void FetchMissingBlocks()
        {
            // Build a list of all the blocks that are missing
            List<byte[]> missingBlocks = new List<byte[]>();
            BtcBlockHeader iter = BtcBlockHeader.GenesisBlock;
            while (iter != null)
            {
                string hash = BtcUtils.BytesToString(iter.Hash);
                if (!_blockLibrary.Contains(hash) && !WaitingForBlock(hash))
                    missingBlocks.Add(iter.Hash);
                iter = iter.Next;
            }

            BtcSocket[] connections = BtcNetwork.CurrentConnections;
            int notBanned = connections.Length - _bannedBlockHosts.Count;
            if (notBanned < 10)
                _bannedBlockHosts.Clear(); // everyone is banned, unban them all
            int missingIndex = 0;
            int requests = 0;
            foreach (BtcSocket con in connections)
            {
                if (con.SendsBlocks == BtcSocket.BlockSendState.SendsBlocks && !_pendingBlockRequests.ContainsKey(con) && !_bannedBlockHosts.Contains(con))
                {
                    int fetchCount = Math.Min(missingBlocks.Count - missingIndex, 500);
                    con.RequestBlocks(missingBlocks, missingIndex, fetchCount);

                    HashSet<string> reqBlocks = new HashSet<string>();
                    for (int i = missingIndex; i < missingIndex + fetchCount; i++)
                        reqBlocks.Add(BtcUtils.BytesToString(missingBlocks[i]));
                    _pendingBlockRequests[con] = new BtcBlockRequest() { LastSeenTime = DateTime.Now, RequestedBlocks = reqBlocks, RequestedCount = fetchCount };

                    requests++;
                    missingIndex += fetchCount;

                    if (missingIndex >= missingBlocks.Count)
                        break;
                }
            }
            if (missingIndex > 0)
                BtcLog.Print("Requested {0} blocks from {1} peers", missingIndex.ToString(), requests.ToString());

            _lastBlockRequest = DateTime.Now;
        }

        void IncommingBlockI(BtcSocket from, BtcBlockHeader header, BtcTransaction[] transactions)
        {
            string hashStr = BtcUtils.BytesToString(header.Hash);
            object obj = new object[] { from, transactions };

            _pendingIncommingBlocksLock.WaitOne();
            if (_pendingIncommingBlocks.ContainsKey(hashStr))
            {
                BtcLog.Print("Duplicate block received {0} from {1} and {2}", hashStr, from.RemoteHost, ((BtcSocket)((object[])_pendingIncommingBlocks[hashStr])[0]).RemoteHost);
            }
            else
                _pendingIncommingBlocks[hashStr] = obj;
            _pendingIncommingBlocksLock.ReleaseMutex();
        }

        void ProcessPendingIncommingBlocks()
        {
            if (_pendingIncommingBlocks.Count > 0)
            {
                _pendingIncommingBlocksLock.WaitOne();
                var incommingBlocks = _pendingIncommingBlocks.ToArray();
                _pendingIncommingBlocks.Clear();
                _pendingIncommingBlocksLock.ReleaseMutex();

                foreach (var kvp in incommingBlocks)
                {
                    object[] p = (object[])kvp.Value;
                    BtcSocket from = (BtcSocket)p[0];
                    BtcTransaction[] transactions = (BtcTransaction[])p[1];
                    if (_pendingBlockRequests.ContainsKey(from))
                    {
                        _pendingBlockRequests[from].LastSeenTime = DateTime.Now;
                        _pendingBlockRequests[from].RequestedBlocks.Remove(kvp.Key);
                        if (_pendingBlockRequests[from].RequestedBlocks.Count <= 0)
                            _pendingBlockRequests.Remove(from);
                    }

                    SaveBlock(kvp.Key, transactions);
                }
            }
            else
            {
                List<BtcSocket> remove = new List<BtcSocket>();
                foreach (var kvp in _pendingBlockRequests)
                {
                    TimeSpan ts = DateTime.Now - kvp.Value.LastSeenTime;
                    if (ts.TotalSeconds > 60)
                    {
                        BtcLog.Print("Block Request timeout from host {0}. Putitng {1} blocks back into the pool", kvp.Key.RemoteHost, kvp.Value.RequestedBlocks.Count.ToString());
                        if (kvp.Value.RequestedBlocks.Count == kvp.Value.RequestedCount)
                            _bannedBlockHosts.Add(kvp.Key);
                        remove.Add(kvp.Key);
                    }
                }
                foreach (BtcSocket s in remove)
                    _pendingBlockRequests.Remove(s);
            }
        }

        void SaveBlock(string hashStr, BtcTransaction[] transactions)
        {
            if (!_blockLibrary.Contains(hashStr))
            {
                FileStream blockFile = File.Create(_dataPath + "/blocks/" + hashStr + ".block");

                BinaryWriter bw = new BinaryWriter(blockFile);

                foreach (BtcTransaction tx in transactions)
                    tx.Write(bw);

                bw.Close();

                _blockLibrary.Add(hashStr);
            }
        }

        void LoadHeaders()
        {
            try
            {
                FileStream fs = File.OpenRead(_dataPath + "/blocks.headers");
                BinaryReader br = new BinaryReader(fs);

                while (fs.Position < fs.Length)
                {
                    BtcBlockHeader bh = new BtcBlockHeader(br);
                    AddHeader(bh);
                }
                br.Close();
            }
            catch (Exception)
            {
            }
        }

        void DoDiskSync()
        {
            bool dirtyBlocks = false;
            int blockIndex = 0;
            BtcBlockHeader iter = BtcBlockHeader.GenesisBlock.Next;
            while (iter != null)
            {
                if (iter.Dirty)
                {
                    dirtyBlocks = true;
                    break;
                }
                iter = iter.Next;
                blockIndex++;
            }

            if (dirtyBlocks)
            {
                // At least one block is dirty, starting at iter
                FileStream fs = File.OpenWrite(_dataPath + "/blocks.headers");
                fs.Seek(blockIndex * 81, SeekOrigin.Begin);
                BinaryWriter bw = new BinaryWriter(fs);

                while (iter != null)
                {
                    iter.Write(bw);
                    iter.Dirty = false;
                    iter = iter.Next;
                }

                bw.Close();
            }

            _lastDiskSync = DateTime.Now;
        }

        void AddHeader(BtcBlockHeader header)
        {
            // Link any other blocks we can
            if (_chainFragments.Count > 0)
            {
                List<BtcBlockHeader> remove = new List<BtcBlockHeader>();
                foreach (BtcBlockHeader fragment in _chainFragments)
                {
                    if (BtcUtils.HashEquals(fragment.Hash, header.PrevHash))
                    {
                        if (fragment.Next != null)
                        {
                            // Fragment already has a next block, but this new block is pointing to it as previous
                            if (BtcUtils.HashEquals(fragment.Next.Hash, header.Hash))
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
                            fragment.Next = header;
                            header.Prev = fragment;
                        }
                    }
                    else if (BtcUtils.HashEquals(header.Hash, fragment.PrevHash))
                    {
                        header.Next = fragment;
                        fragment.Prev = header;
                        remove.Add(fragment);
                    }
                }
                foreach (BtcBlockHeader r in remove)
                    _chainFragments.Remove(r);
            }

            // check the fragment against the main chain
            if (BtcUtils.HashEquals(_mainChain.Hash, header.PrevHash))
            {
                // Attach here
                _mainChain.Next = header;
                header.Prev = _mainChain;

                // Go to the end of the chain
                while (_mainChain.Next != null)
                    _mainChain = _mainChain.Next;

                // Recount block numbers
                _knownHeight++;
            }
            else
            {
                // Orphaned fragment, put it in the list for later
                _chainFragments.Add(header);
            }
        }

        BtcBlockHeader FindHeader(byte[] hash)
        {
            BtcBlockHeader iter = _mainChain;
            while (iter != null)
            {
                if (BtcUtils.HashEquals(hash, iter.Hash))
                    return iter;
                iter = iter.Prev;
            }
            return null;
        }

        void CountChain()
        {
            uint headers = 0;

            BtcBlockHeader iter = _mainChain;
            while (iter.Prev != null)
            {
                headers++;
                iter = iter.Prev;
            }

            _knownHeight = headers;
        }

        #endregion

        #region Block Archiving
        void ArchiveBlocks()
        {
            string prefix = _dataPath + "/blocks/";
            
            // Get all blocks in the directory
            string[] rawBlocks = Directory.GetFiles(prefix, "*.block", SearchOption.TopDirectoryOnly);

            List<string> blockFiles = new List<string>();
            foreach (string s in rawBlocks)
            {
                string blockName = s.Substring(prefix.Length);
                blockFiles.Add(blockName);
            }
            blockFiles.Sort();

            for (int i = 0; i < blockFiles.Count;)
            {
                List<string> likeFiles = new List<string>();
                string twoBytes = blockFiles[i].Substring(0, 2);
                likeFiles.Add(blockFiles[i]);
                for (int j = i + 1; j < blockFiles.Count; j++)
                {
                    if (blockFiles[j].StartsWith(twoBytes))
                        likeFiles.Add(blockFiles[j]);
                    else
                        break;
                }
                i += likeFiles.Count;

                // Load the archive if it exists
                string archivePath = prefix + twoBytes + ".archive";
                Dictionary<string, long> archive = LoadArchive(archivePath);

                // Open the decompressed file generated by loading the archive
                FileStream archiveStream = File.Open(archivePath + "D", FileMode.OpenOrCreate, FileAccess.ReadWrite);
                BinaryWriter bw = new BinaryWriter(archiveStream);
                archiveStream.Seek(0, SeekOrigin.End);

                // Write all the blocks we have into this file
                foreach (string file in likeFiles)
                {
                    string hash = file.Substring(0, 64);
                    if (!archive.ContainsKey(hash))
                    {
                        byte[] fileData = File.ReadAllBytes(prefix + file);

                        bw.Write(BtcUtils.StringToBytes(hash));
                        bw.Write(fileData.Length);
                        archive[hash] = archiveStream.Position;
                        bw.Write(fileData);

                        File.Delete(prefix + file);
                    }
                }

                // Compress the archive
                CompressArchive(archivePath, archiveStream);

                // Delete the decompressed file
                File.Delete(archivePath + "D");

                // Write the archive index
                File.WriteAllLines(archivePath + "L", archive.Keys.ToArray());
            }
        }

        Dictionary<string, long> LoadArchive(string archiveName)
        {
            Dictionary<string, long> archive = new Dictionary<string, long>();
            
            if (File.Exists(archiveName))
            {                
                string decompressedFile = archiveName + "D";
                if (File.Exists(decompressedFile))
                    File.Delete(decompressedFile);

                FileStream decomp = File.Create(decompressedFile);

                FileStream comp = File.OpenRead(archiveName);
                DeflateStream ds = new DeflateStream(decomp, CompressionMode.Decompress);
                ds.CopyTo(decomp);
                ds.Close();

                comp.Close();

                BinaryReader br = new BinaryReader(File.OpenRead(decompressedFile));

                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    byte[] hash = br.ReadBytes(32);
                    int dataLen = br.ReadInt32();

                    archive.Add(BtcUtils.BytesToString(hash), br.BaseStream.Position);

                    br.BaseStream.Seek(dataLen, SeekOrigin.Current);
                }
                br.Close();
            }

            return archive;
        }

        void CompressArchive(string archivePath, FileStream archiveStream)
        {
            archiveStream.Seek(0, SeekOrigin.Begin);
            FileStream archive = File.Create(archivePath);
            DeflateStream ds = new DeflateStream(archive, CompressionMode.Compress);
            archiveStream.CopyTo(ds);
            archiveStream.Close();
            ds.Close();
        }
        #endregion
    }
}
