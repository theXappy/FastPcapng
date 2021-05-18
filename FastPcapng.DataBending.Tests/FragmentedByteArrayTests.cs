using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FastPcapng.DataBending.Tests
{
    [TestClass]
    public class FragmentedByteArrayTests
    {
        [TestMethod]
        public void Ctor_ValidSource_EqualsToSource()
        {
            // Arrange
            byte[] source = Enumerable.Range(0, 16).Select(integer => (byte) integer).ToArray();

            // Act
            var fba = new FragmentedByteArray(source);

            // Assert
            Assert.IsTrue(fba.ToArray().SequenceEqual(source));
        }

        [TestMethod]
        public void Append_SomeBytes_Inserted()
        {
            // Arrange
            byte[] source = Enumerable.Range(0, 16).Select(integer => (byte) integer).ToArray();
            var fba = new FragmentedByteArray(source);
            byte[] appended = Enumerable.Range(30, 34).Select(integer => (byte) integer).ToArray();

            // Act
            fba.Append(appended);

            // Assert
            Assert.IsTrue(fba.ToArray().SequenceEqual(source.Concat(appended)));
        }

        [TestMethod]
        public void Prepend_SomeBytes_Inserted()
        {
            // Arrange
            byte[] source = Enumerable.Range(0, 16).Select(integer => (byte) integer).ToArray();
            var fba = new FragmentedByteArray(source);
            byte[] prepended = Enumerable.Range(30, 34).Select(integer => (byte) integer).ToArray();

            // Act
            fba.Prepend(prepended);

            // Assert
            Assert.IsTrue(fba.ToArray().SequenceEqual(prepended.Concat(source)));
        }

        [TestMethod]
        public void Insert_SingleByteAtMiddle_Inserted()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[1] {0x03});

            // Assert
            byte[] expected = new byte[5] {0x01, 0x02, 0x03, 0x04, 0x05};
            byte[] results = fba.ToArray();
            Assert.IsTrue(results.SequenceEqual(expected));
        }

        [TestMethod]
        public void Insert_SingleByteAtSamePositionTwice_Inserted()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x05, 0x06};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[1] {0x04});
            fba.Insert(2, new byte[1] {0x03});

            // Assert
            byte[] expected = new byte[6] {0x01, 0x02, 0x03, 0x04, 0x05, 0x06};
            byte[] results = fba.ToArray();
            Assert.IsTrue(results.SequenceEqual(expected));
        }

        [TestMethod]
        public void Insert_SingleByteAtSequentialPositionTwice_Inserted()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x05, 0x06};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[1] {0x03});
            fba.Insert(3, new byte[1] {0x04});

            // Assert
            byte[] expected = new byte[6] {0x01, 0x02, 0x03, 0x04, 0x05, 0x06};
            byte[] results = fba.ToArray();
            Assert.IsTrue(results.SequenceEqual(expected));
        }

        [TestMethod]
        public void Insert_SingleByteAtIndex0_Inserted()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(0, new byte[1] {0x00});

            // Assert
            byte[] expected = new byte[5] {0x00, 0x01, 0x02, 0x03, 0x04};
            byte[] results = fba.ToArray();
            Assert.IsTrue(results.SequenceEqual(expected));
        }

        [TestMethod]
        public void Insert_MultipleBytesAtMiddle_Inserted()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[2] {0x03, 0x06});

            // Assert
            byte[] expected = new byte[6] {0x01, 0x02, 0x03, 0x06, 0x04, 0x05};
            byte[] results = fba.ToArray();
            Assert.IsTrue(results.SequenceEqual(expected));
        }

        [TestMethod]
        public void Enumeration_SingleFrag_EqualsToSource()
        {
            // Arrange
            byte[] source = new byte[5] {0x01, 0x02, 0x03, 0x04, 0x05};

            // Act
            var fba = new FragmentedByteArray(source);

            // Assert
            int index = 0;
            foreach (byte b in fba)
            {
                Assert.AreEqual(b, source[index],
                    $"Bytes at index {index} where different. Got: {b}, Expected: {source[index]}");
                index++;
            }
        }

        [TestMethod]
        public void Enumeration_AppendedOnce_EqualsToSource()
        {
            // Arrange
            byte[] source = new byte[5] {0x01, 0x02, 0x03, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);
            byte[] appended = new byte[5] {0x11, 0x12, 0x13, 0x14, 0x15};

            // Act
            fba.Append(appended);

            // Assert
            IEnumerable<byte> expected = source.Concat(appended);
            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Enumeration_AfterMiddleInsertion_EqualsToSource()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[1] {0x03});

            // Assert
            IEnumerable<byte> expected = new byte[] {0x01, 0x02, 0x03, 0x04, 0x05};

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Enumeration_AfterTwoMiddleInsertions_EqualsToSource()
        {
            // Arrange
            byte[] source = new byte[4] {0x01, 0x02, 0x05, 0x06};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Insert(2, new byte[1] {0x04});
            fba.Insert(2, new byte[1] {0x03});

            // Assert
            IEnumerable<byte> expected = new byte[] {0x01, 0x02, 0x03, 0x04, 0x05, 0x06};

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Remove_MiddleItemSingleFrag_Removed()
        {
            // Arrange
            byte[] source = new byte[5] {0x01, 0x02, 0x03, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Remove(2, 1);

            // Assert
            IEnumerable<byte> expected = new byte[] {0x01, 0x02, 0x04, 0x05};

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Debug.WriteLine($"Got: {b}, Expected: {nextExpected}");
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Remove_RemoveInsertedItemMiddle_Removed()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x04, 0x05};
            var fba = new FragmentedByteArray(source);
            fba.Insert(2, new byte[] {0x03});

            // Act
            fba.Remove(2, 1);

            // Assert
            IEnumerable<byte> expected = source;

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Remove_RemoveInsertedItemStart_Removed()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);
            fba.Insert(0, new byte[] {0x00});

            // Act
            fba.Remove(0, 1);

            // Assert
            IEnumerable<byte> expected = source;

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void Remove_RemoveInsertedItemEnd_Removed()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);
            fba.Insert(4, new byte[] {0x05});

            // Act
            fba.Remove(4, 1);

            // Assert
            IEnumerable<byte> expected = source;

            int index = 0;
            foreach (byte b in fba)
            {
                byte nextExpected = expected.ElementAt(index);
                Assert.AreEqual(b, nextExpected,
                    $"Bytes at index {index} where different. Got: {b}, Expected: {nextExpected}");
                index++;
            }
        }

        [TestMethod]
        public void IndexerGet_SingleFragArray_Works()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};

            // Act
            var fba = new FragmentedByteArray(source);

            // Assert
            for (int i = 0; i < fba.Length; i++)
            {
                Assert.AreEqual(fba[i], source[i]);
            }
        }

        [TestMethod]
        public void IndexerSet_SingleFragArray_Works()
        {
            // Arrange
            byte[] source = new byte[4]; // Zeroes
            byte[] expected = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            for (int i = 0; i < expected.Length; i++)
            {
                fba[i] = expected[i];
            }

            // Assert
            for (int i = 0; i < fba.Length; i++)
            {
                Assert.AreEqual(fba[i], expected[i]);
            }
        }



        [TestMethod]
        public void CopyToOverload1_SingleFragCopyEverything_AllCopied()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            byte[] copyToMe = new byte[4];
            fba.CopyTo(copyToMe);

            // Assert
            Assert.IsTrue(copyToMe.SequenceEqual(source));
        }

        [TestMethod]
        public void CopyToOverload2_SingleFragCopyEverything_AllCopied()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            byte[] copyToMe = new byte[4];
            fba.CopyTo(0, copyToMe, 0, copyToMe.Length);

            // Assert
            Assert.IsTrue(copyToMe.SequenceEqual(source));
        }

        [TestMethod]
        public void CopyToOverload2_SingleFragCopyFirstHalf_AllCopied()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            byte[] copyToMe = new byte[2];
            fba.CopyTo(0, copyToMe, 0, copyToMe.Length);

            // Assert
            Assert.IsTrue(copyToMe.SequenceEqual(source.Take(2)));
        }


        [TestMethod]
        public void CopyToOverload2_SingleFragCopySecondHalf_AllCopied()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            byte[] copyToMe = new byte[2];
            fba.CopyTo(2, copyToMe, 0, copyToMe.Length);

            // Assert
            Assert.IsTrue(copyToMe.SequenceEqual(source.Skip(2).Take(2)));
        }

        [TestMethod]
        public void Swap_SingleBytesSingleFrags_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Swap(0,1,3,1);

            // Assert
            byte[] expected = new byte[] {0x04, 0x02, 0x03, 0x01};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_SingleBytesSingleFragsReverseOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Swap(3,1,0,1);

            // Assert
            byte[] expected = new byte[] {0x04, 0x02, 0x03, 0x01};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_BytesPairsSingleFrag_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Swap(0,2,2,2);

            // Assert
            byte[] expected = new byte[] {0x03, 0x04, 0x01, 0x02};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BytesPairsSingleFragReverseOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02, 0x03, 0x04};
            var fba = new FragmentedByteArray(source);

            // Act
            fba.Swap(2,2,0,2);

            // Assert
            byte[] expected = new byte[] {0x03, 0x04, 0x01, 0x02};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_BlocksFillingFragsCompletely_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02};
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] {0x03, 0x04});

            // Act
            fba.Swap(0,2,2,2);

            // Assert
            byte[] expected = new byte[] {0x03, 0x04, 0x01, 0x02};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_BlocksFillingFragsCompletelyReversedOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02};
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] {0x03, 0x04});

            // Act
            fba.Swap(2,2,0,2);

            // Assert
            byte[] expected = new byte[] {0x03, 0x04, 0x01, 0x02};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_BlocksNotFillingFragsCompletely_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02,0x03};
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] {0x04, 0x05, 0x06});

            // Act
            fba.Swap(0,2,3,2);

            // Assert
            byte[] expected = new byte[] {0x04, 0x05, 0x03, 0x01, 0x02, 0x06};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BlocksNotFillingFragsCompletelyReversedOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] {0x01, 0x02,0x03};
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] {0x04, 0x05, 0x06});

            // Act
            fba.Swap(3,2,0,2);

            // Assert
            byte[] expected = new byte[] {0x04, 0x05, 0x03, 0x01, 0x02, 0x06};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }


        [TestMethod]
        public void Swap_BlocksNotFillingFragsCompletelyStartingInMiddle_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02, 0x03 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x04, 0x05, 0x06 });

            // Act
            fba.Swap(1, 2, 4, 2);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x05, 0x06, 0x04, 0x02, 0x03};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BlocksNotFillingFragsCompletelyStartingInMiddleReversedOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02, 0x03 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x04, 0x05, 0x06 });

            // Act
            fba.Swap(4, 2, 1, 2);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x05, 0x06, 0x04, 0x02, 0x03};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossTwoFragsEach_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });

            // Act
            fba.Swap(1, 2, 5, 2);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x06, 0x07, 0x04,0x05,0x02,0x03,0x08};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossTwoFragsEachOrderReversed_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });

            // Act
            fba.Swap(5, 2, 1, 2);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x06, 0x07, 0x04,0x05,0x02,0x03,0x08};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        
        
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossThreeFragsEach_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });
            fba.Append(new byte[] { 0x09, 0x0A });
            fba.Append(new byte[] { 0x0B, 0x0C });

            // Act
            fba.Swap(1, 4, 7, 4);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x08, 0x09, 0x0a, 0x0b, 0x06, 0x07, 0x02, 0x03, 0x04, 0x05, 0x0c };
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossThreeFragsEachReversedOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });
            fba.Append(new byte[] { 0x09, 0x0A });
            fba.Append(new byte[] { 0x0B, 0x0C });

            // Act
            fba.Swap(7, 4, 1, 4);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x08, 0x09, 0x0a, 0x0b, 0x06, 0x07, 0x02, 0x03, 0x04, 0x05, 0x0c };
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossThreeFragsEachDifferentLengths_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });
            fba.Append(new byte[] { 0x09, 0x0A });
            fba.Append(new byte[] { 0x0B, 0x0C });

            // Act
            fba.Swap(1, 4, 7, 5);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x06, 0x07, 0x02, 0x03, 0x04, 0x05};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
        [TestMethod]
        public void Swap_BytesPairsSpanningAcrossThreeFragsEachDifferentLengthsReversedOrder_Swapped()
        {
            // Arrange
            byte[] source = new byte[] { 0x01, 0x02 };
            var fba = new FragmentedByteArray(source);
            fba.Append(new byte[] { 0x03, 0x04 });
            fba.Append(new byte[] { 0x05, 0x06 });
            fba.Append(new byte[] { 0x07, 0x08 });
            fba.Append(new byte[] { 0x09, 0x0A });
            fba.Append(new byte[] { 0x0B, 0x0C });

            // Act
            fba.Swap(7, 5, 1, 4);

            // Assert
            byte[] expected = new byte[] { 0x01, 0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x06, 0x07, 0x02, 0x03, 0x04, 0x05};
            Assert.IsTrue(fba.ToArray().SequenceEqual(expected));
        }
    }
}
