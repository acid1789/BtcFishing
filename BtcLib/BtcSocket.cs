using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;

namespace BtcLib
{
    class BtcSocket
    {
        const uint MainNetworkID = 0xD9B4BEF9;
        const uint ProtocolVersion = 60002;
        const ulong NetworkServices = 1;    // For now only supporting NODE_NETWORK
        Socket _socket;
        bool _verrified;
        byte[] _pendingData;
        int _pendingDataOffset;

        public uint RemoteProtocolVersion { get; private set; }
        public ulong RemoteServices { get; private set; }
        public ulong RemoteTimeStamp { get; private set; }
        public string RemoteSubVersion { get; private set; }
        public int RemoteBlockHeight { get; private set; }

        public event Action<BtcSocket, BtcNetworkAddress> OnNodeDiscovered;

        public BtcSocket()
        {
            _pendingData = new byte[1024 * 4];
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public bool Connect(string remoteHost, int remotePort)
        {
            // Connect to the host
            _socket.Connect(remoteHost, remotePort);
            if (!_socket.Connected)
                return false;

            // Send version packet
            SendVersionPacket();

            // Wait for version response
            _verrified = false;
            while (!_verrified)
            {
                if (!Update())
                    break;
            }

            // Request all other nodes that the remote side knows about
            SendPacket("getaddr", new byte[0]);


            return _socket.Connected;
        }

        public bool Update()
        {
            if (!_socket.Connected)
                return false;

            if (_socket.Available > 0)
            {
                int bytesRead = _socket.Receive(_pendingData, _pendingDataOffset, _pendingData.Length - _pendingDataOffset, SocketFlags.None);
                _pendingDataOffset += bytesRead;
            }

            if (_pendingDataOffset > 0)
            {
                int consumedBytes = ProcessPackets();
                int remainingBytes = _pendingDataOffset - consumedBytes;
                if (remainingBytes > 0)
                    Buffer.BlockCopy(_pendingData, consumedBytes, _pendingData, 0, remainingBytes);
                _pendingDataOffset = remainingBytes;
            }

            return true;
        }

        #region Packet Processing
        int ProcessPackets()
        {
            MemoryStream ms = new MemoryStream(_pendingData);
            BinaryReader br = new BinaryReader(ms);

            uint magic = br.ReadUInt32();
            if (magic != MainNetworkID)
            {
                // This is not a bitcoin packet start, try to find one
                bool found = false;
                for (long i = 1; i < _pendingDataOffset - 4; i++)
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
                    return _pendingDataOffset;
                }
            }

            byte[] cmdBytes = br.ReadBytes(12);
            int payloadSize = br.ReadInt32();
            int checksum = br.ReadInt32();

            if (payloadSize + ms.Position <= _pendingDataOffset)
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

        void ProcessCommand(string command, byte[] payload)
        {
            Console.WriteLine("ProcessCommand: " + command);

            switch (command)
            {
                case "version": ProcessVersion(payload); break;
                case "verack": ProcessVerack(); break;
                case "addr": ProcessAddr(payload); break;
                default:
                    Console.WriteLine("ProcessCommand - Unhandled command: " + command);
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

            byte svl = br.ReadByte();
            byte[] sv = br.ReadBytes(svl);
            RemoteSubVersion = Encoding.ASCII.GetString(sv);
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

            byte count = br.ReadByte();
            for (int i = 0; i < count; i++)
            {
                BtcNetworkAddress addr = BtcNetworkAddress.Read(br, true);
                OnNodeDiscovered?.Invoke(this, addr);
            }
        }
        #endregion

        int GenerateChecksum(byte[] data)
        {
            SHA256 sha256 = SHA256Managed.Create();
            byte[] hash = sha256.ComputeHash(sha256.ComputeHash(data));

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
            BtcUtils.PrintBytes(packetData);
            _socket.Send(packetData);
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



        public static bool operator ==(BtcSocket socket, BtcNetworkAddress address)
        {
            IPEndPoint ep = (IPEndPoint)socket._socket.RemoteEndPoint;
            byte[] remoteBytes = ep.Address.GetAddressBytes();
            if (ep.AddressFamily == AddressFamily.InterNetworkV6)
            {
                byte[] testBytes = address.GetIPv6Bytes();
                bool match = true;
                for (int i = 0; i < 12; i++)
                {
                    if (remoteBytes[i] != testBytes[i])
                    {
                        match = false;
                        break;
                    }
                }
                return match;
            }
            else
            {

                byte[] testBytes = address.GetIPv4Bytes();
                bool match = true;
                for (int i = 0; i < 4; i++)
                {
                    if (remoteBytes[i] != testBytes[i])
                    {
                        match = false;
                        break;
                    }
                }
                return match;
            }
            return true;
        }

        public static bool operator !=(BtcSocket socket, BtcNetworkAddress address)
        {
            return !(socket == address);
        }
    }

    class BtcNetworkAddress
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
