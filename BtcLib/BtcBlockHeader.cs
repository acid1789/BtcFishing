using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BtcLib
{
    public class BtcBlockHeader
    {
        static BtcBlockHeader s_GenesisBlock;

        int _version;
        byte[] _hash;
        byte[] _prevBlock;
        byte[] _merkleRoot;
        uint _timestamp;
        uint _difficulty;
        uint _nonce;
        long _transactionCount;

        public BtcBlockHeader Prev { get; set; }
        public BtcBlockHeader Next { get; set; }
        public bool Dirty { get; set; }

        public byte[] PrevHash { get { return _prevBlock; } }

        BtcBlockHeader() { }

        public BtcBlockHeader(BinaryReader br)
        {
            _version = br.ReadInt32();
            _prevBlock = br.ReadBytes(32);
            _merkleRoot = br.ReadBytes(32);
            _timestamp = br.ReadUInt32();
            _difficulty = br.ReadUInt32();
            _nonce = br.ReadUInt32();
            _transactionCount = BtcUtils.ReadVarInt(br);

            byte[] hash = Hash;
        }

        public byte[] Hash
        {
            get
            {
                if (_hash == null)
                {
                    MemoryStream ms = new MemoryStream();
                    BinaryWriter bw = new BinaryWriter(ms);

                    bw.Write(_version);
                    bw.Write(_prevBlock);
                    bw.Write(_merkleRoot);
                    bw.Write(_timestamp);
                    bw.Write(_difficulty);
                    bw.Write(_nonce);

                    byte[] data = ms.ToArray();
                    _hash = BtcUtils.DSha256(data);
                    bw.Close();
                }
                return _hash;
            }
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(_version);
            bw.Write(_prevBlock);
            bw.Write(_merkleRoot);
            bw.Write(_timestamp);
            bw.Write(_difficulty);
            bw.Write(_nonce);
            BtcUtils.WriteVarInt(bw, _transactionCount);
        }

        public static BtcBlockHeader GenesisBlock
        {
            get
            {
                if (s_GenesisBlock == null)
                {                    
                    s_GenesisBlock = new BtcBlockHeader()
                    {
                        _version = 1,
                        _prevBlock = new byte[32],
                        _merkleRoot = new byte[] { 0x3b, 0xa3, 0xed, 0xfd, 0x7a, 0x7b, 0x12, 0xb2, 0x7a, 0xc7, 0x2c, 0x3e, 0x67, 0x76, 0x8f, 0x61, 0x7f, 0xc8, 0x1b, 0xc3, 0x88, 0x8a, 0x51, 0x32, 0x3a, 0x9f, 0xb8, 0xaa, 0x4b, 0x1e, 0x5e, 0x4a },
                        _timestamp = 1231006505,
                        _difficulty = 0x1d00ffff,
                        _nonce = 2083236893,
                        _transactionCount = 1
                    };                    

                    string hash = BtcUtils.BytesToString(s_GenesisBlock.Hash);
                    BtcLog.Print("Genesis block created: " + hash);
                }
                return s_GenesisBlock;
            }
        }
    }
}
