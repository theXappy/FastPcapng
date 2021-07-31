using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using FastPcapng.DataBending;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;
using log4net;
using log4net.Util;

namespace FastPcapng.Internal
{
    /// <summary>
    /// A collection of Enhanced Packet Blocks
    /// </summary>
    public class EnhancedBlocksCollection
    {
        private ILog _logger = LogManager.GetLogger(typeof(EnhancedBlocksCollection));

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
                _logger.DebugExt(()=>$"Offsets.getter invoked. Collection Version: {_version}, Hints Version: {_hintsVersion}");
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
                _logger.DebugExt(()=>$"Lengths.getter invoked. Collection Version: {_version}, Hints Version: {_hintsVersion}");
                if (_version != _hintsVersion)
                {
                    UpdatePacketsHints();
                }
                return _lengthsHints;
            }
        }

        public int Count
        {
            get
            {
                _logger.DebugExt(()=>$"Count.getter invoked. Collection Version: {_version}, Hints Version: {_hintsVersion}");
                return Offsets.Count;
            }
        }

        private void UpdatePacketsHints()
        {
            _logger.DebugExt(()=>$"UpdatePacketsHints invoked. Collection Version: {_version}, Hints Version: {_hintsVersion}");
            (_offsetsHints, _lengthsHints) = _fastReader.ReadEpbOffsetsLengths(_fba);
            _hintsVersion = _version;
        }

        public void Remove(int index)
        {
            _logger.DebugExt(()=>$"Remove invoked. Index: {index}");
            
            int offset = Offsets[index];
            int len = Lengths[index];

            _fba.Remove(offset,len);
            _version++;
            _logger.DebugExt(()=>$"Remove finished. Index: {index}, New Collection Version: {_version}");
        }

        public byte[] GetBlockRaw(int index)
        {
            _logger.DebugExt(()=>$"GetBlockRaw invoked. Index: {index}");
            int offset = Offsets[index];
            int len = Lengths[index];

            byte[] packetBlock = new byte[len];
            _fba.CopyTo(offset, packetBlock, 0, len);
            _logger.DebugExt(()=>$"GetBlockRaw finished. Index: {index}. {len} bytes were read from offset {offset}");
            return packetBlock;
        }
        public (BaseBlock.Types type, uint length, byte[] body) GetBlockPartialParsed(int index)
        {
            _logger.DebugExt(()=>$"GetBlockPartialParsed Invoked. Index: {index}.");
            int offset = Offsets[index];
            int len = Lengths[index];

            byte[] header = new byte[8];
            _fba.CopyTo(offset, header, 0, header.Length);
            var (blockType, blockLen) = _fastReader.ReadBlockTypeLen(header, 0);

            byte[] blockBody = new byte[len - 12];
            _fba.CopyTo(offset + 8, blockBody, 0, len - 12);
            _logger.DebugExt(()=>$"GetBlockPartialParsed finished. Index: {index}. {len} bytes were read from offset {offset}. Parsed Type {blockType}, Parsed Length: {blockLen}");
            return (blockType, blockLen, blockBody);
        }

        public EnhancedPacketBlock GetBlockParsed(int index)
        {
            _logger.DebugExt(()=>$"GetBlockParsed Invoked. Index: {index}.");
            var (type, _, body) = GetBlockPartialParsed(index);
            // TODO: Reverse bytes bool
            var baseBlock = new BaseBlock(type, body, false);
            var epb = EnhancedPacketBlock.Parse(baseBlock, null);
            _logger.DebugExt(()=>$"GetBlockParsed Finished. Index: {index}.");
            return epb;
        }

        public void Update(int index, EnhancedPacketBlock newEpb)
        {
            _logger.DebugExt(()=>$"Update Invoked. Index: {index}.");
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
            _logger.DebugExt(()=>$"Update finished. Index: {index}. New Collection Version: {_version}");
        }

        public void Prepend(EnhancedPacketBlock newEpb)
        {
            _logger.DebugExt(()=>$"Prepend invoked.");
            // TODO: Reverse bytes bool
            byte[] epbData = newEpb.ConvertToByte(false, null);
            _fba.Prepend(epbData);
            _version++;
            _logger.DebugExt(()=>$"Prepend finished. New Collection Version: {_version}");
        }

        public void Append(EnhancedPacketBlock newEpb)
        {
            _logger.DebugExt(()=>$"Append invoked.");
            // TODO: Reverse bytes bool
            byte[] epbData = newEpb.ConvertToByte(false, null);
            _fba.Append(epbData);
            _version++;
            _logger.DebugExt(()=>$"Append finished. New Collection Version: {_version}");
        }

        public void InsertRaw(int index, byte[] epbData)
        {
            _logger.DebugExt(()=>$"InsertRaw invoked. Inserting {epbData.Length} bytes. Index: {index}");
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

            _logger.DebugExt(()=>$"InsertRaw finished. Inserted {epbData.Length} bytes. Index: {index}. New Collection Version: {_version}");
        }

        // TODO: Reverse order bool
        public void Insert(int index, EnhancedPacketBlock newEpb)
        {
            _logger.DebugExt(()=>$"Insert invoked. Index: {index}");
            InsertRaw(index, newEpb.ConvertToByte(false, null));
            _logger.DebugExt(()=>$"Insert finished. Index: {index}, New Collection Version: {_version}");
        }


        public void CopyTo(Stream s)
        {
            Console.WriteLine("CopyTo Invoked.");
            _fba.CopyTo(s);
            Console.WriteLine("CopyTo finished.");
        }

        public void Swap(in int index1, in int index2)
        {
            Console.WriteLine($"Swap Invoked. Indexes: {index1} <-> {index2}");
            int offset1 = Offsets[index1];
            int len1 = Lengths[index1];

            int offset2 = Offsets[index2];
            int len2 = Lengths[index2];

            Console.WriteLine($"Swap. Indexes: {index1} <-> {index2}, found lengths: {len1} <-> {len2}");
            _fba.Swap(offset1, len1, offset2, len2);
            
            // Invalidate offsets/lengths only if we shifted some blocks.
            // This inly happens when the sizes mismatch.
            if (len1 != len2)
            {
                _version++;
            }
            Console.WriteLine($"Swap finished. Indexes: {index1} <-> {index2}, lengths: {len1} <-> {len2}, New Collection Version: {_version}");
        }
    }
}
