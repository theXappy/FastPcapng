using System.Collections.Generic;
using System.IO;
using FastPcapng.Internal;
using Haukcode.PcapngUtils.Common;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;
using Haukcode.PcapngUtils.PcapNG.CommonTypes;
using Haukcode.PcapngUtils.PcapNG.OptionTypes;

namespace FastPcapng
{
    /// <summary>
    /// Represents a complete pcapng file in memory
    /// No file operations occur when modifying an instance of this class
    /// </summary>
    public class MemoryPcapng
    {
        private byte[] _sectionHeader;

        private List<InterfaceDescriptionBlock> _interfaceDescriptionBlocks;

        private EnhancedBlocksCollection _enhancedPacketBlocksBytes;

        private bool _reverseByteOrder;

        /// <summary>
        /// List of network interfaces in the capture
        /// </summary>
        public List<InterfaceDescriptionBlock> Interfaces => _interfaceDescriptionBlocks;

        public MemoryPcapng()
        {
            _reverseByteOrder = false;

            var secHeaderBlock = new SectionHeaderBlock(SectionHeaderBlock.MagicNumbers.Identical, 1, 0, -1,
                new SectionHeaderOption());
            _sectionHeader = secHeaderBlock.ConvertToByte(_reverseByteOrder, null);

            _interfaceDescriptionBlocks = new List<InterfaceDescriptionBlock>()
            {
                new InterfaceDescriptionBlock(LinkTypes.Ethernet, 65535,
                    new InterfaceDescriptionOption("Fake interface 0"))
            };

            EnhancedPacketBlock dummyPacket = new EnhancedPacketBlock(0, new TimestampHelper(0, 0), 1,
                new byte[] { 0x00 }, new EnhancedPacketOption());
            _enhancedPacketBlocksBytes = new EnhancedBlocksCollection(dummyPacket.ConvertToByte(_reverseByteOrder, null));
        }

        public MemoryPcapng(byte[] sectionHeader, List<InterfaceDescriptionBlock> interfaceDescriptionBlocks, byte[] enhancedPacketBlocksBytes, bool reverseByteOrder)
        {
            _sectionHeader = sectionHeader;
            _interfaceDescriptionBlocks = interfaceDescriptionBlocks;
            _enhancedPacketBlocksBytes = new EnhancedBlocksCollection(enhancedPacketBlocksBytes);
            _reverseByteOrder = reverseByteOrder;
        }

        /// <summary>
        /// Remove a packet from the capture
        /// </summary>
        /// <param name="index">0-based index of the packet in the packets list</param>
        public void RemovePacket(int index) => _enhancedPacketBlocksBytes.Remove(index);
        /// <summary>
        /// Add a packet to the beginning of the capture
        /// </summary>
        /// <param name="epb">Packet structure</param>
        public void PrependPacket(EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Prepend(epb);
        /// <summary>
        /// Addes a packet to the end of the capture
        /// </summary>
        /// <param name="epb">Packet structure</param>
        public void AppendPacket(EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Append(epb);
        /// <summary>
        /// Insert a packet in a specific location of the packets list
        /// </summary>
        /// <param name="index">0-based index to insert into</param>
        /// <param name="epb">Packet structure</param>
        public void InsertPacket(int index, EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Insert(index, epb);
        /// <summary>
        /// Overwrite a packet at a specific location of the packets list
        /// </summary>
        /// <param name="index">0-based index to insert into</param>
        /// <param name="epb">Replacement packet structure</param>
        public void UpdatePacket(int index, EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Update(index, epb);
        /// <summary>
        /// Swap two packets within the packets list 
        /// </summary>
        /// <param name="index1">Index of first packet</param>
        /// <param name="index2">Index of second packet</param>
        public void SwapPackets(int index1, int index2) => _enhancedPacketBlocksBytes.Swap(index1, index2);
        /// <summary>
        /// Move a packet eithin the packets list
        /// </summary>
        /// <param name="fromIndex">Index of the packet to move</param>
        /// <param name="toIndex">New index for the packet</param>
        public void MovePacket(int fromIndex, int toIndex)
        {
            var block = _enhancedPacketBlocksBytes.GetBlockRaw(fromIndex);

            RemovePacket(fromIndex);
            _enhancedPacketBlocksBytes.InsertRaw(toIndex, block);
        }
        /// <summary>
        /// Read a packet from the packets list.
        /// The returned object contains a copy of the data and modifying it doesn't not modify the packet in the capture.
        /// </summary>
        /// <param name="index">Index of the packet to read</param>
        /// <returns>Parsed packet block with a copy of the packet's data. Modifying this object doesn't affect the original data in the capture.</returns>
        public EnhancedPacketBlock GetPacket(int index) => _enhancedPacketBlocksBytes.GetBlockParsed(index);

        /// <summary>
        /// Write the complete pcapng file to an output stream
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteTo(Stream stream)
        {
            stream.Write(_sectionHeader);
            foreach (InterfaceDescriptionBlock iface in _interfaceDescriptionBlocks)
            {
                stream.Write(iface.ConvertToByte(_reverseByteOrder, (ex) => throw ex));
            }
            _enhancedPacketBlocksBytes.CopyTo(stream);
        }

        /// <summary>
        /// Parse a pcapng file from an input stream
        /// </summary>
        /// <param name="stream">Input stream</param>
        /// <param name="reverseByteOrder">Byte order</param>
        /// <returns>In-memory copy of the pcapng read from the stream</returns>
        public static MemoryPcapng ParsePcapng(Stream stream, bool reverseByteOrder = false)
        {
            using BinaryReader binReader = new BinaryReader(stream);

            var fastBinReader = new FastPcapngBinReader();

            // Section header
            var (firstBlockType, firstBlockLen) = fastBinReader.ReadBlockTypeLen(binReader);
            if (firstBlockType != BaseBlock.Types.SectionHeader)
            {
                // Error: First block must be section header
                return null;
            }
            byte[] sectionHeader = new byte[firstBlockLen];
            binReader.BaseStream.Seek(0, SeekOrigin.Begin);
            binReader.Read(sectionHeader, 0, (int)firstBlockLen);

            // Interfaces
            List<InterfaceDescriptionBlock> interfaces = fastBinReader.ReadInterfaceDescBlocks(binReader);

            // Packets
            byte[] packets = new byte[stream.Length - stream.Position];
            stream.Read(packets, 0, packets.Length);

            return new MemoryPcapng(sectionHeader, interfaces, packets, reverseByteOrder);
        }

        /// <summary>
        /// Parses a pcapng from a file
        /// </summary>
        /// <param name="path">Pcapng file path</param>
        /// <param name="reverseByteOrder">Byte order</param>
        /// <returns>In-memory copy of the pcapng in the file</returns>
        public static MemoryPcapng ParsePcapng(string path, bool reverseByteOrder = false)
        {
            using FileStream stream = File.OpenRead(path);
            return ParsePcapng(stream, reverseByteOrder);
        }
    }
}

