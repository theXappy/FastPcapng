using System.Collections.Generic;
using System.IO;
using FastPcapng.DataBending;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;

namespace FastPcapng.Internal
{
    public class EnhancedBlocksCollection
    {
        private FragmentedByteArray _fba;
        private FastPcapngBinReader _fastReader = new FastPcapngBinReader();

        private int _version = 0;

        /// <summary>
        /// Don't use it, use the 'Offsets' property
        /// </summary>
        private List<int> _offsetsHints;
        private List<int> _lengthsHints;
        private int _hintsVersion = -1;

        public EnhancedBlocksCollection(byte[] enhancedPacketBlocksBytes)
        {
            _fba = new FragmentedByteArray(enhancedPacketBlocksBytes);
        }

        public IReadOnlyList<int> Offsets
        {
            get
            {
                if (_version != _hintsVersion)
                {
                    UpdatePacketsHints();
                }
                return _offsetsHints;
            }
        }
        public IReadOnlyList<int> Lengths
        {
            get
            {
                if (_version != _hintsVersion)
                {
                    UpdatePacketsHints();
                }
                return _lengthsHints;
            }
        }

        public int Count => Offsets.Count;

        private void UpdatePacketsHints()
        {
            (_offsetsHints, _lengthsHints) = _fastReader.ReadEpbOffsetsLengths(_fba);
            _hintsVersion = _version;
        }

        public void Remove(int index)
        {
            int offset = Offsets[index];
            int len = Lengths[index];

            _fba.Remove(offset,len);
            _version++;
        }

        public byte[] GetBlockRaw(int index)
        {
            int offset = Offsets[index];
            int len = Lengths[index];

            byte[] packetBlock = new byte[len];
            _fba.CopyTo(offset, packetBlock, 0, len);
            return packetBlock;
        }
        public (BaseBlock.Types type, uint length, byte[] body) GetBlockPartialParsed(int index)
        {
            int offset = Offsets[index];
            int len = Lengths[index];

            byte[] header = new byte[8];
            _fba.CopyTo(offset, header, 0, header.Length);
            var (blockType, blockLen) = _fastReader.ReadBlockTypeLen(header, 0);

            byte[] blockBody = new byte[len - 12];
            _fba.CopyTo(offset + 8, blockBody, 0, len - 12);
            return (blockType, blockLen, blockBody);
        }

        public EnhancedPacketBlock GetBlockParsed(int index)
        {
            var (type, _, body) = GetBlockPartialParsed(index);
            // TODO: Reverse bytes bool
            var baseBlock = new BaseBlock(type, body, false);
            var epb = EnhancedPacketBlock.Parse(baseBlock, null);
            return epb;
        }

        public void Update(int index, EnhancedPacketBlock newEpb)
        {
            // TODO: Reverse bytes bool
            byte[] epbData = newEpb.ConvertToByte(false, null);

            int offset = Offsets[index];
            int len = Lengths[index];

            if (len == epbData.Length)
            {
                _fba.Update(offset, epbData);
            }
            else
            {
                _fba.Remove(offset,len);
                _fba.Insert(offset, epbData);
                _version++;
            }
        }

        public void Prepend(EnhancedPacketBlock newEpb)
        {
            // TODO: Reverse bytes bool
            byte[] epbData = newEpb.ConvertToByte(false, null);
            _fba.Prepend(epbData);
            _version++;
        }

        public void Append(EnhancedPacketBlock newEpb)
        {
            // TODO: Reverse bytes bool
            byte[] epbData = newEpb.ConvertToByte(false, null);
            _fba.Append(epbData);
            _version++;
        }

        public void InsertRaw(int index, byte[] epbData)
        {

            if (index == 0)
            {
                _fba.Prepend(epbData);
                _version++;
                return;
            }
            if (index == Offsets.Count)
            {
                _fba.Append(epbData);
                _version++;
                return;
            }

            int offset = Offsets[index];
            _fba.Insert(offset, epbData);
            _version++;
        }

        // TODO: Reverse order bool
        public void Insert(int index, EnhancedPacketBlock newEpb) => InsertRaw(index, newEpb.ConvertToByte(false, null));


        public void CopyTo(Stream s)
        {
            _fba.CopyTo(s);
        }

        public void Swap(in int index1, in int index2)
        {
            int offset1 = Offsets[index1];
            int len1 = Lengths[index1];

            int offset2 = Offsets[index2];
            int len2 = Lengths[index2];

            _fba.Swap(offset1, len1, offset2, len2);
            
            // Invalidate offsets/lengths only if we shifted some blocks.
            // This inly happens when the sizes mismatch.
            if (len1 != len2)
            {
                _version++;
            }
        }
    }
}
