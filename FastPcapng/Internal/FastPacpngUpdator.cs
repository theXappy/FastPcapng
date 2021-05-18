using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Haukcode.PcapngUtils.PcapNG.BlockTypes;

namespace FastPcapng.Internal
{
    public class FastPacpngUpdator
    {

        /// <summary>
        /// Replaces a packet with a new packet
        /// </summary>
        /// <param name="packetBlockOffset">Offset in the file where the block begins</param>
        /// <param name="newPacket">New packet block</param>
        public byte[] UpdatePacket(byte[] pcapngBytes, long packetBlockOffset, EnhancedPacketBlock newPacket)
        {
            // TODO: Reverse Byte order not always false probably...
            byte[] newEpbData = newPacket.ConvertToByte(false, (ex) => Debug.WriteLine("LOL!" + ex));

            using (MemoryStream fileStream = new MemoryStream(pcapngBytes, true))
            using (BinaryReader binReader = new BinaryReader(fileStream)) {
                // Navigate to the overridden block start
                long actualOffset = fileStream.Seek(packetBlockOffset, SeekOrigin.Begin);
                // Make sure seek succeeded
                if (actualOffset != packetBlockOffset) {
                    throw new Exception($"Couldn't seek to offset {packetBlockOffset}");
                }

                // Read next 8 bytes of the block we are overriding:
                BaseBlock.Types type = (BaseBlock.Types)binReader.ReadUInt32();
                uint overridenBlockLen = binReader.ReadUInt32();
                if (type != BaseBlock.Types.EnhancedPacket) {
                    throw new Exception($"Expected an ENHANCED PACKET BLOCK (val:{BaseBlock.Types.EnhancedPacket}) in the given offset but got: {type}");
                }
                if (overridenBlockLen < newEpbData.Length) {
                    return UpdatePacketLonger(newEpbData, fileStream, overridenBlockLen);
                }
                if (overridenBlockLen > newEpbData.Length) {
                    return UpdatePacketShorter(newEpbData, fileStream, pcapngBytes, overridenBlockLen);
                }
                else {
                    // Lengths match exactly
                    fileStream.Seek(-4, SeekOrigin.Current);
                    fileStream.Write(newEpbData, 4, newEpbData.Length - 4);
                    return pcapngBytes;
                }
            }
        }

        /// <summary>
        /// Update packet where the replacement block is shorter than the existing block
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte[] UpdatePacketShorter(byte[] newEbpData, Stream stream, byte[] underlyingArray, uint overridenBlockLen)
        {
            uint diff = overridenBlockLen - (uint)newEbpData.Length;
            if (diff % 4 != 0) {
                throw new NotImplementedException($"Trying to override with smaller packet but length diff is not multiply of 4");
            }
            // The trick: Adding unknown 'Option' blocks at the end of the packet

            // Starting by just copying everything except:
            // * first 4 bytes of Block type (stays the same)
            // * 4 bytes of block length (stays the same)
            // * 4 last bytes of 'Options', which are set to 0x00_00_00_00 (end indicator)
            // * last 4 bytes - repeated block length (stays the same)
            stream.Write(newEbpData, 8, newEbpData.Length - 16);

            // Number of DWORDs to add: the difference calculated + 1 for the removed end indicator in Options
            uint numOfDwordsToPad = diff / 4 + 1;
            for (uint i = 0; i < numOfDwordsToPad; i++) {
                // Option Type:
                // * 0x00AA (unknown) if middle
                // * 0x0000 (end indicator) if last
                if (i == numOfDwordsToPad - 1) {
                    stream.WriteByte(0x00);
                }
                else {
                    stream.WriteByte(0xAA);
                }
                stream.WriteByte(0x00);
                // Option Length: 0
                stream.WriteByte(0x00);
                stream.WriteByte(0x00);
            }

            return underlyingArray;
        }

        /// <summary>
        /// Update packet where the replacement block is longer than the existing block
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte[] UpdatePacketLonger(byte[] newEpbData, Stream stream, uint overridenBlockLen)
        {
            long lenDiff = newEpbData.Length - overridenBlockLen;
            byte[] output = new byte[stream.Length + lenDiff];

            long overridenBlockOffset = stream.Position - 8;

            // Write blocks before overriden block
            stream.Seek(0, SeekOrigin.Begin);
            stream.Write(output, 0, (int)overridenBlockOffset);

            // Write replacement block
            newEpbData.CopyTo(output, overridenBlockOffset);

            // Write all blocks after the overriden block
            long restOffset = overridenBlockOffset + newEpbData.Length;
            stream.Seek(overridenBlockLen, SeekOrigin.Current);
            stream.Write(output, (int)restOffset, (int)(stream.Length - stream.Position));

            return newEpbData;
        }


    }
}