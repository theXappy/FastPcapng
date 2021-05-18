using System.Collections.Generic;
using System.IO;
using FastPcapng.Internal;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;

namespace FastPcapng
{
    public class MemoryPcapng
    {
        private byte[] _sectionHeader;

        private List<InterfaceDescriptionBlock> _interfaceDescriptionBlocks;
        
        private EnhancedBlocksCollection _enhancedPacketBlocksBytes;
        
        private bool _reverseByteOrder;

        public List<InterfaceDescriptionBlock> Interfaces => _interfaceDescriptionBlocks;

        public MemoryPcapng(byte[] sectionHeader, List<InterfaceDescriptionBlock> interfaceDescriptionBlocks, byte[] enhancedPacketBlocksBytes, bool reverseByteOrder)
        {
            _sectionHeader = sectionHeader;
            _interfaceDescriptionBlocks = interfaceDescriptionBlocks;
            _enhancedPacketBlocksBytes = new EnhancedBlocksCollection(enhancedPacketBlocksBytes);
            _reverseByteOrder = reverseByteOrder;
        }
        
        public void RemovePacket(int index) => _enhancedPacketBlocksBytes.Remove(index);
        public void PrependPacket(EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Prepend(epb);
        public void AppendPacket(EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Append(epb);
        public void InsertPacket(int index, EnhancedPacketBlock epb) => _enhancedPacketBlocksBytes.Insert(index,epb);
        public void SwapPackets(int index1, int index2) => _enhancedPacketBlocksBytes.Swap(index1, index2);
        public void MovePacket(int fromIndex, int toIndex)
        {
            var block = _enhancedPacketBlocksBytes.GetBlockRaw(fromIndex);
            // Order of operations is crucial since removing from/inserting to the earlier offset 
            // will decrease/increase the later index by 1.
            // Doing so in the "right order" allows us to nelgect recalculating the later offset.
            if(fromIndex > toIndex)
            {
                RemovePacket(fromIndex);
                _enhancedPacketBlocksBytes.InsertRaw(toIndex, block);
            }
            else
            {
                _enhancedPacketBlocksBytes.InsertRaw(toIndex, block);
                RemovePacket(fromIndex);
            }
        }
        public EnhancedPacketBlock GetPacket(int index) => _enhancedPacketBlocksBytes.GetBlockParsed(index);

        public void WriteTo(Stream stream)
        {
            stream.Write(_sectionHeader);
            foreach (InterfaceDescriptionBlock iface in _interfaceDescriptionBlocks)
            {
                stream.Write(iface.ConvertToByte(_reverseByteOrder,(ex)=>throw ex));
            }
            _enhancedPacketBlocksBytes.CopyTo(stream);
        }

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

        public static MemoryPcapng ParsePcapng(string path, bool reverseByteOrder = false)
        {
            using FileStream stream = File.OpenRead(path);
            return ParsePcapng(stream, reverseByteOrder);
        }
    }
}

