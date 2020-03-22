// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using System.Linq;
using Spreads.Native;

#pragma warning disable 618

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class DataBlockTests
    {
        [Test, Explicit("output")]
        public void SizeOf()
        {
            ObjectLayoutInspector.TypeLayout.PrintLayout<Vec>();
            ObjectLayoutInspector.TypeLayout.PrintLayout<DataBlock>(false);
        }

        [Test]
        public void WrappedLookup()
        {
            var count = 10_000;
            var arr = Enumerable.Range(0, count).Select(x => (long)x).ToArray();
            
            var r = ArrayMemory<long>.Create(arr, 
                0, arr.Length, externallyOwned: true);
            var keys = RetainedVec.Create(r, 0, r.Length);
            var values = keys.Slice(0, count, true);

            var block = DataBlock.SeriesCreate(keys, values, count);
            for (int i = 0; i < count; i++)
            {
                var ii = (long)i;
                Assert.AreEqual(i, block.LookupKey(ref ii, Lookup.EQ));
            }
            
            block.Dispose();
        }

        [Test]
        public void CouldDoubleSeriesCapacity()
        {
            // Debug this test to see buffer management errors during finalization, normal test run survives them in VS

            var block = DataBlock.SeriesCreate();
            Assert.AreEqual(0, block.RowCount);

            block.SeriesIncreaseCapacity<int, int>();

            Assert.AreEqual(block.RowCapacity, Settings.MIN_POOLED_BUFFER_LEN);

            var keys = block.RowKeys._memoryOwner as ArrayMemory<int>;
            var vals = block.Values._memoryOwner as ArrayMemory<int>;

            var slice = block.Values.Slice(0, 1);

            Assert.NotNull(keys);
            Assert.NotNull(vals);

            Assert.IsTrue(keys.IsPoolable);
            Assert.IsFalse(keys.IsPooled);

            block.SeriesIncreaseCapacity<int, int>();

            // keys were returned to the pool after doubling capacity
            Assert.IsTrue(keys.IsPooled);

            // values were borrowed via Slice
            Assert.IsFalse(vals.IsPooled);

            slice.Dispose();

            Assert.IsTrue(vals.IsPooled);

            Assert.AreEqual(block.RowCapacity, Settings.MIN_POOLED_BUFFER_LEN * 2);

            for (int i = 0; i < 20; i++)
            {
                block.SeriesIncreaseCapacity<int, int>();
                Console.WriteLine(block.RowCapacity);
            }

            block.Dispose();
        }
    }
}
