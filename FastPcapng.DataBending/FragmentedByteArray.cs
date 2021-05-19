using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastPcapng.DataBending
{
    public class FragmentedByteArray : IEnumerable<byte>
    {
        public List<FlexyByteArray> Frags = new();
        public int Length => Frags.Sum(frag => frag.Length);

        public FragmentedByteArray(byte[] arr)
        {
            Frags.Add(new FlexyByteArray(arr));
        }
        public void Append(byte[] data)
        {
            Frags.Add(new FlexyByteArray(data));
        }

        public void Prepend(byte[] data)
        {
            Frags.Insert(0, new FlexyByteArray(data));
        }

        public void Insert(int offset, byte[] data)
        {
            // TODO: Could be faster if saving offsets with FlexyByteArrays
            int currBlockStartOffset;
            int currBlockEndOffset = 0;
            for (var i = 0; i < Frags.Count; i++) {
                currBlockStartOffset = currBlockEndOffset;
                currBlockEndOffset = currBlockStartOffset + Frags[i].Length;

                if (offset == currBlockStartOffset) {
                    // Inserting BEFORE current block (might happen only for first block?)
                    Frags.Insert(i, new FlexyByteArray(data));
                    return;
                }
                if (offset > currBlockStartOffset && offset < currBlockEndOffset) {
                    // Inserting in the MIDDLE of the current block. Split required.
                    FlexyByteArray currFrag = Frags[i];
                    FlexyByteArray firstHalf = currFrag.CutTo(offset - currBlockStartOffset);
                    FlexyByteArray secondHalf = currFrag.CutFrom(offset - currBlockStartOffset);

                    // Add "sandwich" of split halfs and the data to insert
                    Frags.Insert(i, secondHalf);
                    Frags.Insert(i, new FlexyByteArray(data));
                    Frags.Insert(i, firstHalf);

                    // Remove instance of broken-down block that we just split
                    Frags.Remove(currFrag);
                    return;

                }
                if (offset == currBlockEndOffset) {
                    // Inserting AFTER current block
                    Frags.Insert(i + 1, new FlexyByteArray(data));
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
            // TODO: Could be faster if saving offsets with FlexyByteArrays
            int currBlockStartOffset;
            int currBlockEndOffset = 0;

            List<FlexyByteArray> fragsToRemoveList = new List<FlexyByteArray>();
            for (var i = 0; i < Frags.Count; i++) {
                FlexyByteArray currFrag = Frags[i];
                currBlockStartOffset = currBlockEndOffset;
                currBlockEndOffset = currBlockStartOffset + Frags[i].Length;

                if (offset == currBlockStartOffset) {
                    // Start of deleted data is at the start of current frag
                    if (amount < currFrag.Length) {
                        // Also end of deletion is within current frag (but it's not ALL of the frag)
                        // So let's just advance the start
                        currFrag.Start += amount;
                        currFrag.Length -= amount;
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
                    FlexyByteArray firstHalf = currFrag.CutTo(offset - currBlockStartOffset);
                    FlexyByteArray secondHalf = currFrag.CutFrom(offset - currBlockStartOffset);

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
            foreach (FlexyByteArray fba in Frags) {
                fba.CopyTo(s);
            }
        }

        public void CopyTo(byte[] b) => CopyTo(0, b, 0, b.Length);
        public void CopyTo(int sourceOffset, byte[] b, int dstOffset, int length)
        {
            int offset = 0;
            int taken = 0;
            foreach (FlexyByteArray fba in Frags)
            {
                if (offset + fba.Length > sourceOffset)
                {
                    int fragSourceOffset = 0;
                    if (sourceOffset > offset)
                    {
                        fragSourceOffset = sourceOffset - offset;
                    }

                    int amountLeftToCopy = length - taken;
                    int availableInFrag = fba.Length - fragSourceOffset;

                    int numToTake = Math.Min(amountLeftToCopy, availableInFrag);
                    fba.CopyTo(fragSourceOffset, b, dstOffset, numToTake);
                    dstOffset += numToTake;
                    taken += numToTake;
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

        public byte[] ToArray()
        {
            byte[] output = new byte[Length];
            int offset = 0;
            foreach (FlexyByteArray frag in Frags) {
                frag.CopyTo(output, offset, frag.Length);
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
                        return Frags[j][i - currBlockStartOffset];
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
                        FlexyByteArray fba = Frags[j];
                        fba[i - currBlockStartOffset] = value;
                        return;
                    }
                }

                throw new IndexOutOfRangeException(
                    $"Bad arguments to {nameof(FragmentedByteArray)}[]. i: {i}, Total Length: {Length}");

            }
        }

        #region IEnumerable Impl

        public class FragmentedByteArrayEnumerator : IEnumerator<byte>
        {
            private FragmentedByteArray _ba;
            private IEnumerator<FlexyByteArray> _fragsEnumerator = null;
            private IEnumerator<byte> _currFragEnumerator = null;

            public FragmentedByteArrayEnumerator(FragmentedByteArray ba)
            {
                _ba = ba;
                _fragsEnumerator = _ba.Frags.GetEnumerator();
                _fragsEnumerator.MoveNext();
                _currFragEnumerator = _fragsEnumerator.Current.GetEnumerator();
            }

            public bool MoveNext()
            {
                if (_currFragEnumerator.MoveNext()) {
                    return true;
                }
                // Out of bytes in curr frag enumerator
                if (_fragsEnumerator.MoveNext()) {
                    // Got a new frag, starting getting bytes from it.
                    _currFragEnumerator = _fragsEnumerator.Current.GetEnumerator();
                    _currFragEnumerator.MoveNext();
                    return true;
                }
                // Out of frags
                return false;
            }

            public void Reset()
            {
                _fragsEnumerator?.Dispose();
                _fragsEnumerator = _ba.Frags.GetEnumerator();
                _fragsEnumerator.MoveNext();
                _currFragEnumerator = _fragsEnumerator.Current.GetEnumerator();
            }

            public byte Current => (byte)_currFragEnumerator.Current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _fragsEnumerator?.Dispose();
            }
        }
        public IEnumerator<byte> GetEnumerator() => new FragmentedByteArrayEnumerator(this);
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
                FlexyByteArray frag1 = Frags[fragIndex1];
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
            byte[] earlyBlock = GetSubArray(earlyOffset, earlyLen);
            byte[] laterBlock = GetSubArray(laterOffset, laterLen);

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
