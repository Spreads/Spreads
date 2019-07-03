// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using System;

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class DataBlockTests
    {
        [Test, Explicit("output")]
        public void SizeOf()
        {
            ObjectLayoutInspector.TypeLayout.PrintLayout<DataBlock>();
        }

        [Test]
        public void CouldDoubleSeriesCapacity()
        {
            // Debug this test to see buffer management errors during finalization, normal test run survives them in VS

            var block = DataBlock.Create();
            Assert.AreEqual(0, block.RowLength);

            block.IncreaseSeriesCapacity<int, int>();

            Assert.AreEqual(block.RowKeys.Length, Settings.MIN_POOLED_BUFFER_LEN);

            var keys = block.RowKeys._memorySource as ArrayMemory<int>;
            var vals = block.Values._memorySource as ArrayMemory<int>;

            var slice = block.Values.Slice(0, 1);

            Assert.NotNull(keys);
            Assert.NotNull(vals);

            Assert.IsTrue(keys.IsPoolable);
            Assert.IsFalse(keys.IsPooled);

            block.IncreaseSeriesCapacity<int, int>();

            // keys were returned to the pool after doubling capacity
            Assert.IsTrue(keys.IsPooled);

            // values were borrowed via Slice
            Assert.IsFalse(vals.IsPooled);

            slice.Dispose();

            Assert.IsTrue(vals.IsPooled);

            Assert.AreEqual(block.RowKeys.Length, Settings.MIN_POOLED_BUFFER_LEN * 2);

            for (int i = 0; i < 10; i++)
            {
                block.IncreaseSeriesCapacity<int, int>();
                Console.WriteLine(block.RowKeys.Length);
            }

            block.Dispose();
        }
    }
}
