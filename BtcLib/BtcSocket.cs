using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace BtcLib
{
    public class BtcSocket
    {
        public enum BlockSendState { Unknown, Checking, DoesntSendBlocks, SendsBlocks };
        
        const uint MainNetworkID = 0xD9B4BEF9;
        const uint ProtocolVersion = 70015;
        const ulong NetworkServices = 1;    // For now only supporting NODE_NETWORK
        Socket _socket;
        bool _verrified;
        byte[] _pendingData;
        string _remoteHost;
        int _score;

        DateTime _requestedBlockTime;

        public string RemoteHost { get { return _remoteHost; } }
        public uint RemoteProtocolVersion { get; private set; }
        public ulong RemoteServices { get; private set; }
        public ulong RemoteTimeStamp { get; private set; }
        public string RemoteSubVersion { get; private set; }
        public int RemoteBlockHeight { get; private set; }
        public BlockSendState SendsBlocks { get; private set; }

        public event Action<BtcSocket, BtcNetworkAddress> OnNodeDiscovered;
        public event Action<BtcSocket, BtcNetwork.InventoryType, byte[]> OnInventory;
        public event Action<BtcSocket, BtcBlockHeader> OnHeader;
        public event Action<BtcSocket, BtcBlockHeader, BtcTransaction[]> OnBlock;

        public BtcSocket()
        {
            _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            _socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
        }

        public bool Connect(string remoteHost, int remotePort)
        {
            // Connect to the host
            //BtcLog.Print("Connecting to: " + remoteHost + ":" + remotePort);
            _remoteHost = remoteHost;
            try
            {
                IAsyncResult ares = _socket.BeginConnect(remoteHost, remotePort, null, null);
                bool success = ares.AsyncWaitHandle.WaitOne(2000, true);
                if (success)
                    _socket.EndConnect(ares);
                else
                    throw new Exception("Connection Timed Out");
            }
            catch (Exception) { return false; }
            if (!_socket.Connected)
                return false;

            // Send version packet
            SendVersionPacket();

            // Wait for version response
            _verrified = false;
            DateTime start = DateTime.Now;
            while (!_verrified)
            {
                if (!Update())
                    break;
                
                if ((DateTime.Now - start).TotalSeconds > 2)
                {
                    _socket.Close();
                    break;
                }
            }

            // Request all other nodes that the remote side knows about
            if (_socket.Connected)
            {
                SendPacket("getaddr", new byte[0]);

                SendsBlocks = BlockSendState.Checking;
                List<byte[]> hashes = new List<byte[]>();
                hashes.Add(BtcBlockHeader.GenesisBlock.Hash);
                RequestBlocks(hashes, 0, 1);
            }


            return _socket.Connected;
        }

        public bool Update()
        {
            if (!_socket.Connected)
                return false;

            if (_socket.Available > 0)
            {
                byte[] recvBuffer = new byte[_socket.Available];
                int bytesRead = _socket.Receive(recvBuffer, 0, recvBuffer.Length, SocketFlags.None);

                if (_pendingData != null)
                {
                    byte[] combined = new byte[_pendingData.Length + bytesRead];
                    Buffer.BlockCopy(_pendingData, 0, combined, 0, _pendingData.Length);
                    Buffer.BlockCopy(recvBuffer, 0, combined, _pendingData.Length, bytesRead);
                    _pendingData = combined;
                }
                else
                    _pendingData = recvBuffer;                
            }

            if (_pendingData != null)
            {
                int consumedBytes = ProcessPackets();
                int remainingBytes = _pendingData.Length - consumedBytes;
                if (remainingBytes > 0)
                {
                    byte[] newPending = new byte[remainingBytes];
                    Buffer.BlockCopy(_pendingData, consumedBytes, newPending, 0, remainingBytes);
                    _pendingData = newPending;
                }
                else
                    _pendingData = null;
            }

            if (SendsBlocks == BlockSendState.Checking)
            {
                TimeSpan ts = DateTime.Now - _requestedBlockTime;
                if (ts.TotalSeconds > 15)
                    SendsBlocks = BlockSendState.DoesntSendBlocks;
            }

            return true;
        }

        public bool IsAddress(BtcNetworkAddress address)
        {
            IPEndPoint ep = (IPEndPoint)_socket.RemoteEndPoint;
            byte[] remoteBytes = ep.Address.GetAddressBytes();
            if (remoteBytes.Length == 16)
            {
                bool match = true;
                byte[] testBytes = address.GetIPv6Bytes();
                for (int i = 0; i < remoteBytes.Length; i++)
                {
                    if (testBytes[i] != remoteBytes[i])
                    {
                        match = false;
                        break;
                    }
                }
                return match;
            }
            return false;
        }

        public int Score { get { return _score; } } 
        public void IncrementScore() { _score++; }
        public void DecrementScore() { _score--; }
        #region Packet Processing
        int ProcessPackets()
        {
            try
            {
                MemoryStream ms = new MemoryStream(_pendingData);
                BinaryReader br = new BinaryReader(ms);

                uint magic = br.ReadUInt32();
                if (magic != MainNetworkID)
                {
                    // This is not a bitcoin packet start, try to find one
                    bool found = false;
                    for (long i = 1; i < _pendingData.Length - 4; i++)
                    {
                        ms.Seek(i, SeekOrigin.Begin);
                        magic = br.ReadUInt32();
                        if (magic == MainNetworkID)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // Didn't find a bitcoin packet in what we have, throw it all away
                        return _pendingData.Length;
                    }
                }

                byte[] cmdBytes = br.ReadBytes(12);
                int payloadSize = br.ReadInt32();
                int checksum = br.ReadInt32();

                if (payloadSize + ms.Position <= _pendingData.Length)
                {
                    // We have all the data for this packet
                    byte[] payload = br.ReadBytes(payloadSize);

                    // Hash the payload and check against the header hash
                    int chk = GenerateChecksum(payload);
                    if (chk == checksum)
                    {
                        string command = GetCommandString(cmdBytes);
                        ProcessCommand(command, payload);
                    }
                }
                else
                    return 0;   // Not enough data for this packet yet, don't do anything

                int eaten = (int)ms.Position;
                br.Close();
                return eaten;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        void ProcessCommand(string command, byte[] payload)
        {
            //BtcLog.Print("ProcessCommand: " + command);

            switch (command)
            {
                case "version": ProcessVersion(payload); break;
                case "verack": ProcessVerack(); break;
                case "addr": ProcessAddr(payload); break;
                case "ping": ProcessPing(payload); break;
                case "alert": ProcessAlert(payload); break;
                case "getheaders": ProcessGetHeaders(payload); break;
                case "headers": ProcessHeaders(payload); break;
                case "inv": ProcessInv(payload); break;
                case "encinit": ProcessEncInit(payload); break;
                case "block": ProcessBlock(payload); break;
                default:
                    BtcLog.Print("ProcessCommand - Unhandled command: " + command);
                    break;
            }
        }

        void ProcessVersion(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms);

            RemoteProtocolVersion = br.ReadUInt32();
            RemoteServices = br.ReadUInt64();
            RemoteTimeStamp = br.ReadUInt64();
            BtcNetworkAddress.Read(br, false);
            BtcNetworkAddress.Read(br, false);

            ulong nodeId = br.ReadUInt64();

            RemoteSubVersion = BtcUtils.ReadVarString(br);
            RemoteBlockHeight = br.ReadInt32();
        }

        void ProcessVerack()
        {
            _verrified = true;
        }

        void ProcessAddr(byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms);

            long count = BtcUtils.ReadVarInt(br);
            for (int i = 0; i < count; i++)
            {
                BtcNetworkAddress addr = BtcNetworkAddress.Read(br, true);
                OnNodeDiscovered?.Invoke(this, addr);
            }
        }

        void ProcessPing(byte[] data)
        {
            SendPacket("pong", data);
        }

        void ProcessAlert(byte[] data)
        {
        }

        void ProcessGetHeaders(byte[] data)
        {
            //DateTime start = DateTime.Now;

            BinaryReader br = new BinaryReader(new MemoryStream(data));
            int version = br.ReadInt32();
            long count = BtcUtils.ReadVarInt(br);

            //BtcLog.Print("getheaders: " + count);
            List<byte[]> hashes = new List<byte[]>();
            for (long i = 0; i < count; i++)
            {
                byte[] b = br.ReadBytes(32);
                hashes.Add(b);
                //BtcLog.Print("\t" + BtcUtils.BytesToString(b));
            }
            byte[] stopHash = br.ReadBytes(32);

            BtcBlockHeader location = null;
            foreach (byte[] hash in hashes)
            {
                location = BtcBlockChain.FindBlockHeader(hash);
                if (location != null)
                    break;
            }

            if (location != null)
            {
                List<BtcBlockHeader> headers = new List<BtcBlockHeader>();
                while (headers.Count < 2000 && location.Next != null && !BtcUtils.HashEquals(location.Next.Hash, stopHash))
                {
                    location = location.Next;
                    headers.Add(location);
                }

                MemoryStream ms = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(ms);
                BtcUtils.WriteVarInt(bw, headers.Count);
                foreach (BtcBlockHeader bh in headers)
                    bh.Write(bw);

                SendPacket("headers", ms.ToArray());
                bw.Close();
            }

            //TimeSpan ts = DateTime.Now - start;
            //BtcLog.Print("getheaders command took {0} seconds", ts.TotalSeconds.ToString());
        }

        void ProcessHeaders(byte[] data)
        {
            BtcNetwork.HeaderFetchEnabled = false;

            IncrementScore();
            BinaryReader br = new BinaryReader(new MemoryStream(data));

            long count = BtcUtils.ReadVarInt(br);
            for (long i = 0; i < count; i++)
            {
                BtcBlockHeader header = new BtcBlockHeader(br);
                header.Dirty = true;
                OnHeader?.Invoke(this, header);
            }

            br.Close();
            //BtcBlockChain.ReCount();
            BtcNetwork.HeaderFetchEnabled = true;
        }

        void ProcessInv(byte[] data)
        {
            if (OnInventory != null)
            {
                BinaryReader br = new BinaryReader(new MemoryStream(data));
                long count = BtcUtils.ReadVarInt(br);
                for (long i = 0; i < count; i++)
                {
                    int type = br.ReadInt32();
                    byte[] hash = br.ReadBytes(32);
                    OnInventory.Invoke(this, (BtcNetwork.InventoryType)type, hash);
                }
            }
        }

        void ProcessEncInit(byte[] data)
        {
            BinaryReader br = new BinaryReader(new MemoryStream(data));

            byte[] remotePubKey = br.ReadBytes(33);
            byte cypherType = br.ReadByte();

            BtcLog.Print("encinit receivied from remote host {0}, not supported", _remoteHost);
            _socket.Close();            
        }

        void ProcessBlock(byte[] data)
        {
            SendsBlocks = BlockSendState.SendsBlocks;
            if (OnBlock != null)
            {
                BinaryReader br = new BinaryReader(new MemoryStream(data));
                BtcBlockHeader header = new BtcBlockHeader(br);
                List<BtcTransaction> transactions = new List<BtcTransaction>();
                for (int i = 0; i < header.TransactionCount; i++)
                {
                    BtcTransaction tx = new BtcTransaction(br);
                    transactions.Add(tx);
                }

                OnBlock.Invoke(this, header, transactions.ToArray());
            }
        }
        #endregion

        int GenerateChecksum(byte[] data)
        {
            byte[] hash = BtcUtils.DSha256(data);

            int chk = hash[3] << 24 | hash[2] << 16 | hash[1] << 8 | hash[0];
            return chk;
        }

        string GetCommandString(byte[] cmdBytes)
        {
            string str = "";
            foreach (byte b in cmdBytes)
            {
                if (b == 0)
                    break;
                str += (char)b;
            }

            return str;
        }

        void SendPacket(string command, byte[] payload)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(MainNetworkID);

            byte[] cmdBytes = Encoding.ASCII.GetBytes(command);
            byte[] cmdBytes12 = new byte[12];
            for (int i = 0; i < cmdBytes12.Length; i++)
                cmdBytes12[i] = (i < cmdBytes.Length) ? cmdBytes[i] : (byte)0;

            bw.Write(cmdBytes12);
            bw.Write(payload.Length);
            bw.Write(GenerateChecksum(payload));
            bw.Write(payload);

            byte[] packetData = ms.ToArray();
            //BtcUtils.PrintBytes(packetData);
            try
            {
                _socket.Send(packetData);
            }
            catch (Exception) { }
            bw.Close();
        }

        void SendVersionPacket()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(ProtocolVersion);
            bw.Write(NetworkServices);
            bw.Write(BtcUtils.UnixTime);

            // Write network addresses
            new BtcNetworkAddress().Write(bw, false);
            new BtcNetworkAddress(0).Write(bw, false);

            bw.Write(BtcNetwork.BitcionNodeId);

            byte[] subVersion = Encoding.ASCII.GetBytes("/Satoshi:0.15.0/");
            bw.Write((byte)subVersion.Length);   // sub-version string
            bw.Write(subVersion);
            bw.Write(BtcBlockChain.Height);

            SendPacket("version", ms.ToArray());
            bw.Close();
        }

        public void SendGetHeadersPacket()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(RemoteProtocolVersion);
            BtcUtils.WriteVarInt(bw, 1);
            bw.Write(BtcBlockChain.Tip.Hash);
            bw.Write(new byte[32]);

            SendPacket("getheaders", ms.ToArray());
            bw.Close();
        }

        public void RequestBlocks(List<byte[]> blockHashes, int startIndex, int count)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            
            BtcUtils.WriteVarInt(bw, count);
            for (int i = 0; i < count; i++)
            {
                bw.Write((int)2);   // MSG_BLOCK
                bw.Write(blockHashes[startIndex + i]);
            }

            SendPacket("getdata", ms.ToArray());
            bw.Close();
            _requestedBlockTime = DateTime.Now;
        }
    }

    public class BtcNetworkAddress
    {
        uint _timeStamp;
        ulong _services;
        byte[] _ipAddress;
        ushort _port;

        public BtcNetworkAddress(ulong services = 1)
        {
            _services = services;  // Default to NODE_NETWORK
            _ipAddress = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0, 0, 0, 0 };
            _port = 0;
        }

        public byte[] GetIPv4Bytes()
        {
            byte[] ipv4 = new byte[4];
            ipv4[0] = _ipAddress[12];
            ipv4[1] = _ipAddress[13];
            ipv4[2] = _ipAddress[14];
            ipv4[3] = _ipAddress[15];
            return ipv4;
        }

        public byte[] GetIPv6Bytes()
        {
            return _ipAddress;
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}.{3}", _ipAddress[12], _ipAddress[13], _ipAddress[14], _ipAddress[15]);
        }

        public void Write(BinaryWriter bw, bool inlcudeTimeStamp)
        {
            if (inlcudeTimeStamp)
                bw.Write(_timeStamp);
            bw.Write(_services);
            bw.Write(_ipAddress);
            bw.Write(_port);
        }

        public static BtcNetworkAddress Read(BinaryReader br, bool includeTimeStamp)
        {
            BtcNetworkAddress bna = new BtcNetworkAddress();
            if (includeTimeStamp)
                bna._timeStamp = br.ReadUInt32();
            bna._services = br.ReadUInt64();
            bna._ipAddress = br.ReadBytes(16);
            bna._port = br.ReadUInt16();
            return bna;
        }
    }
}

