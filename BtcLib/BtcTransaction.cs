using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace BtcLib
{
    public class BtcTransaction
    {
        public class TxIn
        {
            /*
            Field Size	Description	Data type	Comments
            36	previous_output	outpoint	The previous output transaction reference, as an OutPoint structure
            1+	script length	var_int	The length of the signature script
             ?	signature script	uchar[]	Computational Script for confirming transaction authorization
            4	sequence	uint32_t	Transaction version as defined by the sender. Intended for "replacement" of transactions when information is updated before inclusion into a block.
            */

            byte[] _fromHash;
            int _index;
            byte[] _script;
            uint _sequence;

            public TxIn(BinaryReader br)
            {
                _fromHash = br.ReadBytes(32);
                _index = br.ReadInt32();
                long scriptLen = BtcUtils.ReadVarInt(br);
                _script = br.ReadBytes((int)scriptLen);
                _sequence = br.ReadUInt32();
            }

            public void Write(BinaryWriter bw)
            {
                bw.Write(_fromHash);
                bw.Write(_index);
                BtcUtils.WriteVarInt(bw, _script.Length);
                bw.Write(_script);
                bw.Write(_sequence);
            }
        }

        public class TxOut
        {
            /*
            Field Size	Description	Data type	Comments
            8	value	int64_t	Transaction Value
            1+	pk_script length	var_int	Length of the pk_script
             ?	pk_script	uchar[]	Usually contains the public key as a Bitcoin script setting up conditions to claim this output.
            */

            long _value;
            byte[] _script;

            public TxOut(BinaryReader br)
            {
                _value = br.ReadInt64();
                long scriptLen = BtcUtils.ReadVarInt(br);
                _script = br.ReadBytes((int)scriptLen);
            }

            public void Write(BinaryWriter bw)
            {
                bw.Write(_value);
                BtcUtils.WriteVarInt(bw, _script.Length);
                bw.Write(_script);
            }
        }

        public class TxWitness
        {
            public TxWitness(BinaryReader br)
            {
                throw new NotImplementedException();
            }
        }

        /*
        Field Size	Description	Data type	Comments
        4	version	int32_t	Transaction data format version (note, this is signed)
        0 or 2	flag	optional uint8_t[2]	If present, always 0001, and indicates the presence of witness data
        1+	tx_in count	var_int	Number of Transaction inputs (never zero)
        41+	tx_in	tx_in[]	A list of 1 or more transaction inputs or sources for coins
        1+	tx_out count	var_int	Number of Transaction outputs
        9+	tx_out	tx_out[]	A list of 1 or more transaction outputs or destinations for coins
        0+	tx_witnesses	tx_witness[]	A list of witnesses, one for each input; omitted if flag is omitted above
        4	lock_time	uint32_t	The block number or timestamp at which this transaction is unlocked:
        Value	Description
        0	Not locked
        < 500000000	Block number at which this transaction is unlocked
        >= 500000000	UNIX timestamp at which this transaction is unlocked
        If all TxIn inputs have final (0xffffffff) sequence numbers then lock_time is irrelevant. Otherwise, the transaction may not be added to a block until after lock_time (see NLockTime)
        */
            int _version;
        short _flag;
        int _lockTime;

        TxIn[] _inputs;
        TxOut[] _outputs;
        TxWitness[] _witnesses;

        public BtcTransaction(BinaryReader br)
        {
            _version = br.ReadInt32();

            byte flag = br.ReadByte();
            if (flag != 0)
                br.BaseStream.Seek(-1, SeekOrigin.Current);
            else
                br.ReadByte();

            long inCount = BtcUtils.ReadVarInt(br);
            List<TxIn> inputs = new List<TxIn>();
            for (long i = 0; i < inCount; i++)
            {
                TxIn tin = new TxIn(br);
                inputs.Add(tin);
            }
            _inputs = inputs.ToArray();

            long outCount = BtcUtils.ReadVarInt(br);
            List<TxOut> outputs = new List<TxOut>();
            for (long i = 0; i < outCount; i++)
            {
                TxOut tout = new TxOut(br);
                outputs.Add(tout);
            }
            _outputs = outputs.ToArray();

            if (flag == 0)
            {
                List<TxWitness> witnesses = new List<TxWitness>();
                for (long i = 0; i < inCount; i++)
                {
                    TxWitness wit = new TxWitness(br);
                    witnesses.Add(wit);
                }
                _witnesses = witnesses.ToArray();
            }

            _lockTime = br.ReadInt32();
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write(_version);
            if (_witnesses != null)
            {
                bw.Write((byte)0);
                bw.Write((byte)1);
            }

            BtcUtils.WriteVarInt(bw, _inputs.Length);
            foreach (TxIn i in _inputs)
                i.Write(bw);

            BtcUtils.WriteVarInt(bw, _outputs.Length);
            foreach (TxOut o in _outputs)
                o.Write(bw);

            if (_witnesses != null)
            {

            }
            bw.Write(_lockTime);
        }
    }
}
