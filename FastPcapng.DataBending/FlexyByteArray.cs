using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FastPcapng.DataBending
{
    [DebuggerDisplay("Start: {Start}, Length: {Length}, Items: {UnderlyingArray[Start]}, {UnderlyingArray[Start+1]}, {UnderlyingArray[Start+2]}")]
    public struct FlexyByteArray : IEnumerable<byte>
    {
        public byte[] UnderlyingArray;
        public int Start;
        public int Length;

        public FlexyByteArray(byte[] underlyingArray, int start, int length)
        {
            UnderlyingArray = underlyingArray;
            Start = start;
            Length = length;
        }

        public FlexyByteArray(byte[] underlyingArray) : this(underlyingArray, 0, underlyingArray.Length)
        {
        }

        public FlexyByteArray CutFrom(int offset)
        {
            if (offset < 0)
            {
                throw new Exception("Can't expand using the Cut function");
            }
            return new FlexyByteArray(UnderlyingArray, Start + offset, Length - offset);
        }
        public FlexyByteArray CutTo(int newLength)
        {
            return new FlexyByteArray(UnderlyingArray, Start, newLength);
        }

        public void CopyTo(Stream s)
        {
            s.Write(UnderlyingArray, Start, Length);
        }

        public void CopyTo(byte[] output, int offset, int fragLength)
        {
            Buffer.BlockCopy(UnderlyingArray, this.Start, output, offset, fragLength);
        }
        public void CopyTo(int srcOffset, byte[] output, int dstOffset, int fragLength)
        {
            Buffer.BlockCopy(UnderlyingArray, this.Start + srcOffset, output, dstOffset, fragLength);
        }

        public class FlexyByteArrayEnumerator : IEnumerator<byte>
        {
            private FlexyByteArray _fba;
            private int offset = -1;

            public FlexyByteArrayEnumerator(FlexyByteArray fba)
            {
                _fba = fba;
            }

            public bool MoveNext()
            {
                if (offset == _fba.Length)
                    return false;
                offset++;
                return offset != _fba.Length;
            }

            public void Reset()
            {
                offset = -1;
            }

            public byte Current => _fba.UnderlyingArray[_fba.Start + offset];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return new FlexyByteArrayEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public byte this[int i]
        {
            get => this.UnderlyingArray[Start + i];
            set => this.UnderlyingArray[Start + i] = value;
        }
    }
}
