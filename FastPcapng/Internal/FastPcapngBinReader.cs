using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FastPcapng.DataBending;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;

namespace FastPcapng.Internal
{
    class FastPcapngBinReader
    {
        /**
         *
         *  Read Enhanced Packet Blocks Offsets
         *
        **/

        public List<long> ReadEpbOffsets(byte[] arr)
        {
            using MemoryStream memStream = new MemoryStream(arr, false);
            return ReadEpbOffsets(memStream);
        }

        public List<long> ReadEpbOffsets(Stream s)
        {
            BinaryReader binReader = new BinaryReader(s);
            return ReadEpbOffsets(binReader);
        }

        public List<long> ReadEpbOffsets(BinaryReader binReader)
        {
            List<long> offsets = new List<long>();
            var innerStream = binReader.BaseStream;
            while (innerStream.Position != innerStream.Length) {
                // Read next 8 bytes of block:
                BaseBlock.Types type = (BaseBlock.Types)binReader.ReadUInt32();
                uint len = binReader.ReadUInt32();
                if (type == BaseBlock.Types.EnhancedPacket) {
                    offsets.Add(innerStream.Position - 8);
                }
                // Advance to next block start
                innerStream.Seek(len - 8, SeekOrigin.Current);
            }
            return offsets;
        }

        public (List<int> offsets, List<int> lengths) ReadEpbOffsetsLengths(FragmentedByteArray fba)
        {
            byte[] blockHeader = new byte[8];

            var offsets = new List<int>();
            var lengths = new List<int>();

            int offset = 0;
            int length = fba.Length;
            while (offset < length)
            {
                // Copy block header
                fba.CopyTo(offset, blockHeader, 0, blockHeader.Length);

                // Parse block header
                var (blockType, blockLen) = ReadBlockTypeLen(blockHeader, 0);

                if (blockType == BaseBlock.Types.EnhancedPacket)
                {
                    offsets.Add(offset);
                    lengths.Add((int)blockLen);
                }

                offset += (int) blockLen;
            }

            return (offsets, lengths);
        }

        /**
         *
         * Basic block parsing - Type and Length
         *
         */

        public (BaseBlock.Types type, uint Length) ReadBlockTypeLen(byte[] arr, int offset)
        {
            using MemoryStream memStream = new MemoryStream(arr, false);
            memStream.Seek(offset, SeekOrigin.Begin);

            return ReadBlockTypeLen(memStream);
        }

        public (BaseBlock.Types type, uint Length) ReadBlockTypeLen(Stream stream)
        {
            BinaryReader binReader = new BinaryReader(stream);
            return ReadBlockTypeLen(binReader);
        }

        public (BaseBlock.Types type, uint Length) ReadBlockTypeLen(BinaryReader binReader)
        {
            return ((BaseBlock.Types) binReader.ReadUInt32(), binReader.ReadUInt32());
        }


        /**
         *
         * Read Interface Description Blocks
         *
        **/

        public List<InterfaceDescriptionBlock> ReadInterfaceDescBlocks(BinaryReader binReader)
        {
            // TODO:
            bool reverseByteOrder = false;

            var stream = binReader.BaseStream;
            List<InterfaceDescriptionBlock> interfaces = new List<InterfaceDescriptionBlock>(10);
            while (stream.Position != stream.Length) {
                // Read next 8 bytes of block:
                long blockOffset = stream.Position;
                BaseBlock.Types type = (BaseBlock.Types)binReader.ReadUInt32();
                if (type == BaseBlock.Types.EnhancedPacket) {
                    // Found a packet block, stopping search
                    stream.Seek(-4, SeekOrigin.Current);
                    break;
                }

                uint len = binReader.ReadUInt32();

                if (type == BaseBlock.Types.InterfaceDescription) {
                    stream.Seek(-8, SeekOrigin.Current);
                    var block = AbstractBlockFactory.ReadNextBlock(binReader, reverseByteOrder, (ex) => Debug.WriteLine("Kek!" + ex));
                    if (block == null || block.BlockType != BaseBlock.Types.InterfaceDescription) {
                        throw new Exception("Block at given position was not parsed to a INTERFACE DESCRIPTION BLOCK");
                    }

                    interfaces.Add(block as InterfaceDescriptionBlock);
                }
                else {
                    // Advance to next block start
                    stream.Seek(len - 8, SeekOrigin.Current);
                }
            }

            return interfaces;
        }
    }
}
