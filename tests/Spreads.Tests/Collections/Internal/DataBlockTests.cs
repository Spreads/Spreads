// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Core.Tests;
using Spreads.Core.Tests.Collections;

#pragma warning disable 618

namespace Spreads.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class DataBlockTests
    {
        [Test, Explicit("output")]
        public void SizeOf()
        {
            ObjectLayoutInspector.TypeLayout.PrintLayout<DataBlock>(false);
            ObjectLayoutInspector.TypeLayout.PrintLayout<Vec>();
        }

        [Test]
        public void SentinelIsEmptyAndDisposed()
        {
            Assert.AreEqual(0, DataBlock.Empty.RowCount);
            Assert.AreEqual(0, DataBlock.Empty.RowCapacity);
            Assert.AreEqual(true, DataBlock.Empty.IsDisposed);
        }

        [Test]
        public void WrappedLookup()
        {
            var count = 10_000;
            var arr = Enumerable.Range(0, count).Select(x => (long) x).ToArray();

            var pm = BuffersTestHelper.CreateFilledRM(count);
            var keys = RetainedVec.Create(pm, 0, count);
            var values = keys.Clone(0, count, true);

            var block = DataBlock.CreateForPanel(keys, values, count);
            for (int i = 0; i < count; i++)
            {
                var ii = (long) i;
                Assert.AreEqual(i, block.LookupKey(ref ii, Lookup.EQ));
            }

            block.Dispose();
        }

        [Test]
        public void CouldDoubleSeriesCapacity()
        {
            // Debug this test to see buffer management errors during finalization, normal test run survives them in VS

            var block = DataBlock.CreateForPanel();
            Assert.AreEqual(0, block.RowCount);

            block.IncreaseRowsCapacity<int, int>();

            Assert.AreEqual(block.RowCapacity, Settings.MIN_POOLED_BUFFER_LEN);

            var keys = block.RowKeys._memoryOwner as PrivateMemory<int>;
            var vals = block.Values._memoryOwner as PrivateMemory<int>;

            var slice = block.Values.Clone(0, 1);

            Assert.NotNull(keys);
            Assert.NotNull(vals);

            Assert.IsTrue(keys.IsPoolable);
            Assert.IsFalse(keys.IsPooled);

            block.IncreaseRowsCapacity<int, int>();

            // keys were returned to the pool after doubling capacity
            Assert.IsTrue(keys.IsPooled);

            // values were borrowed via Slice
            Assert.IsFalse(vals.IsPooled);

            slice.Dispose();

            Assert.IsTrue(vals.IsPooled);

            Assert.AreEqual(block.RowCapacity, Settings.MIN_POOLED_BUFFER_LEN * 2);

            for (int i = 0; i < 20; i++)
            {
                block.IncreaseRowsCapacity<int, int>();
                Console.WriteLine(block.RowCapacity);
            }

            block.Dispose();
        }

        [Test]
        public void CouldCreateForSeries()
        {
            var db = DataBlock.CreateForSeries<long, int>();
            db.RowCount.ShouldBe(0);
            db.RowKeys.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN);
            db.Values.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN);
            db.ColumnCount.ShouldBe(1);
            db.ColumnKeys.Length.ShouldBe(0);

            db.RowKeys.RuntimeTypeId.ShouldBe(TypeHelper<long>.RuntimeTypeId);
            db.Values.RuntimeTypeId.ShouldBe(TypeHelper<int>.RuntimeTypeId);
            db.ColumnKeys.RuntimeTypeId.ShouldBe((RuntimeTypeId)0);

            db.Columns.ShouldBeNull();
            db.IsDisposed.ShouldBeFalse();
            db.IsValid.ShouldBeTrue();
            db.ReferenceCount.ShouldBe(0);
            db.ReferenceCount.ShouldBe(0);

            Assert.Throws<ArrayTypeMismatchException>(() => db.IncreaseRowsCapacity<int, int>());

            Assert.Throws<ArrayTypeMismatchException>(code: () => db.IncreaseRowsCapacity<long, long>());

            db.IncreaseRowsCapacity<long, int>().ShouldBe(Settings.MIN_POOLED_BUFFER_LEN * 2);
            db.RowKeys.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN * 2);
            db.Values.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN * 2);

            var ro = db.RowKeys._memoryOwner!;
            var vo = db.Values._memoryOwner!;
            ro.IsDisposed.ShouldBeFalse();

            db.Dispose();
            db.IsDisposed.ShouldBeTrue();

            ro.IsDisposed.ShouldBeTrue();
            vo.IsDisposed.ShouldBeTrue();

            db.RowKeys.Length.ShouldBe(0);
            db.Values.Length.ShouldBe(0);

            (ro as PrivateMemory<long>).IsPooled.ShouldBeTrue();

            (vo as PrivateMemory<int>).IsPooled.ShouldBeTrue();

            Assert.Throws<ObjectDisposedException>(() => { db.Decrement(); });
        }

        [Test]
        public void CouldCreateForVector()
        {
            var db = DataBlock.CreateForVector<long>();
            db.RowCount.ShouldBe(0);
            db.RowKeys.Length.ShouldBe(0);
            db.Values.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN);
            db.ColumnCount.ShouldBe(1);
            db.ColumnKeys.Length.ShouldBe(0);

            db.RowKeys.RuntimeTypeId.ShouldBe((RuntimeTypeId)0);
            db.Values.RuntimeTypeId.ShouldBe(TypeHelper<long>.RuntimeTypeId);
            db.ColumnKeys.RuntimeTypeId.ShouldBe((RuntimeTypeId)0);

            db.Columns.ShouldBeNull();
            db.IsDisposed.ShouldBeFalse();
            db.IsValid.ShouldBeTrue();
            db.ReferenceCount.ShouldBe(0);

            Assert.Throws<ArrayTypeMismatchException>(() => db.IncreaseRowsCapacity<Index, int>());

            db.IncreaseRowsCapacity<Index, long>().ShouldBe(Settings.MIN_POOLED_BUFFER_LEN * 2);
            db.RowKeys.Length.ShouldBe(0);
            db.Values.Length.ShouldBe(Settings.MIN_POOLED_BUFFER_LEN * 2);

            db.RowKeys._memoryOwner.ShouldBeNull();
            var vo = db.Values._memoryOwner!;

            vo.IsDisposed.ShouldBeFalse();

            db.Dispose();
            db.IsDisposed.ShouldBeTrue();

            vo.IsDisposed.ShouldBeTrue();

            db.RowKeys.Length.ShouldBe(0);
            db.Values.Length.ShouldBe(0);

            (vo as PrivateMemory<long>).IsPooled.ShouldBeTrue();

            Assert.Throws<ObjectDisposedException>(() => { db.Decrement(); });
        }
    }
}
