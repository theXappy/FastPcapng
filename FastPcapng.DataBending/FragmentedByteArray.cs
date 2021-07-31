using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FastPcapng.DataBending
{
    /// <summary>
    /// Represents a byte array which is separated to one or more segments in memory
    /// This should be more efficient implementation to a byte array when multiple resizing operations are expected (appending/prepending/insertion/deletion)
    /// </summary>
    public class FragmentedByteArray : IEnumerable<byte>
    {
        public List<Memory<byte>> Frags = new();
        public int Length => Frags.Sum(frag => frag.Length);

        public FragmentedByteArray(byte[] arr)
        {
            Frags.Add(new Memory<byte>(arr));
        }
        public void Append(byte[] data)
        {
            Frags.Insert(Frags.Count, new Memory<byte>(data));
        }

        public void Prepend(byte[] data)
        {
            Frags.Insert(0, new Memory<byte>(data));
        }

        public void Insert(int offset, byte[] data)
        {
            // TODO: Could be faster if saving offsets with Memory<T>s
            int currBlockStartOffset;
            int currBlockEndOffset = 0;
            for (var i = 0; i < Frags.Count; i++) {
                currBlockStartOffset = currBlockEndOffset;
                currBlockEndOffset = currBlockStartOffset + Frags[i].Length;

                if (offset == currBlockStartOffset) {
                    // Inserting BEFORE current block (might happen only for first block?)
                    Frags.Insert(i, new Memory<byte>(data));
                    return;
                }
                if (offset > currBlockStartOffset && offset < currBlockEndOffset) {
                    // Inserting in the MIDDLE of the current block. Split required.
                    Memory<byte> currFrag = Frags[i];
                    Memory<byte> firstHalf = currFrag[..(offset - currBlockStartOffset)];
                    Memory<byte> secondHalf = currFrag[(offset - currBlockStartOffset)..];

                    // Add "sandwich" of split halfs and the data to insert
                    Frags.Insert(i, secondHalf);
                    Frags.Insert(i, new Memory<byte>(data));
                    Frags.Insert(i, firstHalf);

                    // Remove instance of broken-down block that we just split
                    Frags.Remove(currFrag);
                    return;

                }
                if (offset == currBlockEndOffset) {
                    // Inserting AFTER current block
                    Frags.Insert(i + 1, new Memory<byte>(data));
                    return;
                }
            }

            if(Frags.Count == 0)
            {
                // FBA is empty, just add as first block
                Append(data);
                return;
            }

            throw new ArgumentException(
                $"Bad arguments to {nameof(FragmentedByteArray)}.{nameof(Insert)}(). i: {offset}, Total Length: {Length}");
        }

        public void Remove(int offset, int amount)
        {
            // TODO: Could be faster if saving offsets with Memory<T>s
            int currBlockStartOffset;
            int currBlockEndOffset = 0;

            List<Memory<byte>> fragsToRemoveList = new();
            for (var i = 0; i < Frags.Count; i++) {
                Memory<byte> currFrag = Frags[i];
                currBlockStartOffset = currBlockEndOffset;
                currBlockEndOffset = currBlockStartOffset + Frags[i].Length;

                if (offset == currBlockStartOffset) {
                    // Start of deleted data is at the start of current frag
                    if (amount < currFrag.Length) {
                        // Also end of deletion is within current frag (but it's not ALL of the frag)
                        // So let's just advance the start
                        currFrag = currFrag.Slice(amount);
                        Frags[i] = currFrag; // Immutable bullsh*t
                        break;
                    }
                    else if (amount == currFrag.Length) {
                        // Removing complete frag
                        Frags.Remove(currFrag);
                        break;
                    }
                    else {
                        // Removing all of this frag + more bytes of following frags!
                        // Register current frag for removal
                        fragsToRemoveList.Add(currFrag);

                        // Fixing search paramters and continuing to following frags
                        offset += currFrag.Length;
                        amount -= currFrag.Length;
                        continue;
                    }
                }
                if (offset > currBlockStartOffset && offset < currBlockEndOffset) {
                    // Removing from the middle of this frag. So a split must happen
                    Memory<byte> firstHalf = currFrag[..(offset - currBlockStartOffset)];
                    Memory<byte> secondHalf = currFrag[(offset - currBlockStartOffset)..];

                    // Add "sandwich" of split halfs and the data to insert
                    Frags.Insert(i, secondHalf);
                    Frags.Insert(i, firstHalf);

                    // Remove instance of broken-down block that we just split
                    Frags.Remove(currFrag);

                    // Updating EndOffset to reflect that we have handled the 'firstHalf' frag
                    currBlockEndOffset = currBlockStartOffset + firstHalf.Length;

                    // Now just continuing to i+1 will lead us to the second half which will be treated as 'deletion starts at begining of frag'
                    continue;

                }

            }

            foreach (var fba in fragsToRemoveList) {
                Frags.Remove(fba);
            }

        }

        public void Update(int offset, byte[] data)
        {
            // TOOD: This is naive, but maybe it's good enough
            // Possible improvement: Search for the FlexyByteArray containing the first byte and
            //      1. If the FBA's length == data.Length just replace the Underlying array
            //      2. If the FBA's length != data.Length but it contains all the offsets to modify, split the FBA
            //      2. If the FBA's length != data.length and the offsets aren't all contained in the same FBA - fuck it just Remove+Insert
            Remove(offset, data.Length);
            Insert(offset, data);
        }

        public void CopyTo(Stream s)
        {
            foreach (Memory<byte> fba in Frags) {
                s.Write(fba.Span);
            }
        }

        public void CopyTo(byte[] b) => CopyTo(0, b, 0, b.Length);
        public void CopyTo(int sourceOffset, byte[] b, int dstOffset, int length)
        {
            int offset = 0;
            int taken = 0;
            foreach (Memory<byte> fba in Frags)
            {
                // Check if the start offset is within the current FBA's.
                // Current FBA contains offsets in this range [offset, offset+fba.Length]
                if (sourceOffset < offset + fba.Length)
                {
                    int fragSourceOffset = 0;
                    if (sourceOffset > offset)
                    {
                        fragSourceOffset = sourceOffset - offset;
                    }

                    int amountLeftToCopy = length - taken;
                    int availableInFrag = fba.Length - fragSourceOffset;

                    // Take as much as possible from the current FBA, limited by the amount we still need (amountLeftToCopy)
                    int numToTake = Math.Min(amountLeftToCopy, availableInFrag);
                    fba.Slice(fragSourceOffset, numToTake).CopyTo(new Memory<byte>(b, dstOffset, numToTake));
                    dstOffset += numToTake;
                    taken += numToTake;

                    // Check if we finished reading all data requested
                    if (taken == length)
                    {
                        return;
                    }
                }
                offset += fba.Length;
            }
        }

        public byte[] GetSubArray(int start, int length)
        {
            byte[] output = new byte[length];
            CopyTo(start, output, 0, length);
            return output;
        }

        public byte[] this[Range r]
        {
            get
            {
                int startOffset = r.Start.Value;
                if (r.Start.IsFromEnd) startOffset = Length - startOffset;
                int endOffset = r.End.Value;
                if (r.End.IsFromEnd) endOffset = Length - endOffset;

                byte[] output = new byte[endOffset - startOffset];
                CopyTo(startOffset, output, 0, endOffset - startOffset);
                return output;
            }
        }

        public byte[] ToArray()
        {
            byte[] output = new byte[Length];
            int offset = 0;
            foreach (Memory<byte> frag in Frags) {
                frag.Span.CopyTo(output.AsSpan(offset, frag.Length));
                offset += frag.Length;
            }

            return output;
        }


        public byte this[int i]
        {
            get
            {
                // TODO: Could be faster if saving offsets with FlexyByteArrays
                int currBlockEndOffset = 0;
                for (var j = 0; j < Frags.Count; j++) {
                    var currBlockStartOffset = currBlockEndOffset;
                    currBlockEndOffset = currBlockStartOffset + Frags[j].Length;
                    if (i >= currBlockStartOffset && i < currBlockEndOffset) {
                        return Frags[j].Span[i - currBlockStartOffset];
                    }
                }

                throw new IndexOutOfRangeException(
                    $"Bad arguments to {nameof(FragmentedByteArray)}[]. i: {i}, Total Length: {Length}");
            }
            set
            {
                int currBlockEndOffset = 0;
                for (var j = 0; j < Frags.Count; j++) {
                    var currBlockStartOffset = currBlockEndOffset;
                    currBlockEndOffset = currBlockStartOffset + Frags[j].Length;
                    if (i >= currBlockStartOffset && i < currBlockEndOffset)
                    {
                        // Stuff is immutable so I can't edit the FlxyByteArray itself but editing it's underlying 
                        // array does the trick
                        Memory<byte> fba = Frags[j];
                        fba.Span[i - currBlockStartOffset] = value;
                        return;
                    }
                }

                throw new IndexOutOfRangeException(
                    $"Bad arguments to {nameof(FragmentedByteArray)}[]. i: {i}, Total Length: {Length}");

            }
        }

        #region IEnumerable Impl

        public IEnumerator<byte> GetEnumerator()
        {
            foreach(Memory<byte> frag in Frags)
            {
                for(int i=0;i<frag.Length;i++)
                {
                    yield return frag.Span[i];
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        private bool TryGetExactFrag(int offset, int length, out int outputFragIndex)
        {
            outputFragIndex = 0;

            int currBlockStartOffset;
            int currBlockEndOffset = 0;
            for (var i = 0; i < Frags.Count; i++) {
                currBlockStartOffset = currBlockEndOffset;
                currBlockEndOffset = currBlockStartOffset + Frags[i].Length;

                if (offset == currBlockStartOffset) {
                    // Offset matches beginning of frag!
                    if (Frags[i].Length == length)
                    {
                        // Also the frag's length is exactly as we wanted! Returning this frag's index
                        outputFragIndex = i;
                        return true;
                    }
                    // Frag's length wasn't as we wanted, returning false since no FBA matches our search
                    return false;
                }
                if (offset > currBlockStartOffset && offset < currBlockEndOffset) {
                    // offset is somewhere within the FBA -- Must be at start for us to accept
                    return false;
                }
            }

            return false;
        }

        public void Swap(int offset1,int len1,int offset2,int len2)
        {
            // Short-circuit: If both of the blocks are containing exactly in 2 FlexyByteArrays, It's easier
            if (TryGetExactFrag(offset1, len1, out int fragIndex1) &&
                TryGetExactFrag(offset2, len2, out int fragIndex2))
            {
                Memory<byte> frag1 = Frags[fragIndex1];
                Frags[fragIndex1] = Frags[fragIndex2];
                Frags[fragIndex2] = frag1;
                return;
            }

            // Harder case: At least 1 block is not containing exactly in one frag
            // Sort to "earlier" and "later" blocks
            int earlyOffset, earlyLen;
            int laterOffset, laterLen;
            if (offset1 < offset2)
            {
                earlyOffset = offset1;
                earlyLen = len1;
                laterOffset = offset2;
                laterLen = len2;
            }
            else
            {
                earlyOffset = offset2;
                earlyLen = len2;
                laterOffset = offset1;
                laterLen = len1;
            }

            // Dump block's content to new arrays
            byte[] earlyBlock = this[earlyOffset..(earlyOffset + earlyLen)];
            byte[] laterBlock = this[laterOffset..(earlyOffset+laterLen)];

            // Swap the blocks. If the lengths match it's easier since the later offset doesn't move
            if (earlyLen == laterLen)
            {
                Update(earlyOffset, laterBlock);
                Update(laterOffset, earlyBlock);
            }
            else
            {
                // I want Block2 to end in Offset1 and Block1 to end in Offset2
                // But if the lengths aren't equal after moving the later block to the earlier position the later offset will move (back or forward)
                Remove(earlyOffset, earlyLen);
                Insert(earlyOffset, laterBlock);

                int adjustedLaterOffset = laterOffset + (laterLen - earlyLen);
                Remove(adjustedLaterOffset, laterLen);
                Insert(adjustedLaterOffset, earlyBlock);
            }
        }
    }
}
