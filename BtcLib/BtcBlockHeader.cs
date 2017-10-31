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

        public byte[] PrevHash { get { return _prevBlock; } }

        public BtcBlockHeader(BinaryReader br)
        {
            _version = br.ReadInt32();
            _prevBlock = br.ReadBytes(32);
            _merkleRoot = br.ReadBytes(32);
            _timestamp = br.ReadUInt32();
            _difficulty = br.ReadUInt32();
            _nonce = br.ReadUInt32();
            _transactionCount = BtcUtils.ReadVarInt(br);

            GetHash();
        }

        public byte[] GetHash()
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
                BtcUtils.WriteVarInt(bw, 0);

                _hash = BtcUtils.Sha256(ms.ToArray());
                bw.Close();
            }
            return _hash;
        }
    }
}
