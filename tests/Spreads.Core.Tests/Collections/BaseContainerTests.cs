// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System.Linq;
using System.Runtime.CompilerServices;

#pragma warning disable 618

namespace Spreads.Core.Tests.Collections
{
    [Category("CI")]
    [TestFixture]
    public class BaseContainerTests
    {
        [Test]
        public void CouldTryGetBlockAtSingleChunk()
        {
            var capacity = 100;
            var bc = new BaseContainer<int>();

            var rm = BuffersTestHelper.CreateFilledRM(capacity);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            var block = DataBlock.Create(rowIndex: vs, rowLength: vs.Length / 2);

            bc.Data = block;

            var searchIndex = 40;
            var found = bc.TryGetBlockAt(searchIndex, out var dataBlock, out var ci);
            Assert.IsTrue(found);
            Assert.AreSame(block, dataBlock);
            Assert.AreEqual(searchIndex, ci);

            bc.Dispose();
        }

        [Test]
        public void CouldTryFindBlockAtSingleChunk()
        {
            var capacity = 100;
            var bc = new BaseContainer<long>();

            var rm = BuffersTestHelper.CreateFilledRM(capacity);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            var block = DataBlock.Create(rowIndex: vs, rowLength: vs.Length / 2);

            bc.Data = block;

            var searchIndex = 40L;
            var searchIndexRef = searchIndex;
            var found = DataContainer.TryFindBlockAt(bc.Data, ref searchIndexRef, Lookup.LT, out var c, out var ci, bc._comparer);
            Assert.IsTrue(found);
            Assert.AreSame(block, c);
            Assert.AreEqual(searchIndex - 1, ci);
            Assert.AreEqual(searchIndex - 1, searchIndexRef);

            bc.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldTryFindBlockAtSingleChunkBench()
        {
            var count = (int) TestUtils.GetBenchCount(50_000_000, 50_000);
            var rounds = TestUtils.GetBenchCount(20, 2);

            // for this test capacity is irrelevant - interpolation search hits exact position on first try
            var capacity = count / 100;
            var bc = new BaseContainer<long>();
            var rm = BuffersTestHelper.CreateFilledRM(capacity);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            var block = DataBlock.Create(rowIndex: vs, rowLength: capacity);

            bc.Data = block;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("TryFindChunkAt", count))
                {
                    var m = count / capacity;
                    for (int _ = 0; _ < m; _++)
                    {
                        for (long i = 1; i < capacity; i++)
                        {
                            var searchIndexRef = i;
                            var found = DataContainer.TryFindBlockAt(bc.Data, ref searchIndexRef, Lookup.LE, out var c, out var ci, bc._comparer);
                            if (!found
                                || !ReferenceEquals(block, c)
                                || i != ci
                                || i != searchIndexRef
                            )
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }
            }

            Benchmark.Dump();
            bc.Dispose();
        }

        [Test]
        public void CouldTryGetBlockSingleChunk()
        {
            var capacity = 100;
            var bc = new BaseContainer<long>();
            var rm = BuffersTestHelper.CreateFilledRM(capacity);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            var block = DataBlock.Create(rowIndex: vs, rowLength: vs.Length / 2);

            bc.Data = block;

            var searchIndex = 40L;
            var searchIndexRef = searchIndex;
            var found = bc.TryGetBlock(searchIndexRef, out var c, out var ci);
            Assert.IsTrue(found);
            Assert.AreSame(block, c);
            Assert.AreEqual(searchIndex, ci);
            Assert.AreEqual(searchIndex, searchIndexRef);
            bc.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long running")
#endif
        ]
        public void CouldTryGetBlockSingleChunkBench()
        {
            var count = (int) TestUtils.GetBenchCount(50_000_000, 50_000);
            var rounds = TestUtils.GetBenchCount(20, 2);

            // for this test capacity is irrelevant - interpolation search hits exact position on first try
            var capacity = count / 100;
            var bc = new BaseContainer<long>();

            var rm = BuffersTestHelper.CreateFilledRM(capacity);
            var vs = RetainedVec.Create(rm, 0, rm.Length);

            var block = DataBlock.Create(rowIndex: vs, rowLength: capacity);

            bc.Data = block;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("TryGetBlock", count))
                {
                    var m = count / capacity;
                    for (int _ = 0; _ < m; _++)
                    {
                        for (long i = 0; i < capacity; i++)
                        {
                            var ival = vs.UnsafeReadUnaligned<long>((int) i);
                            var ival2 = vs.UnsafeReadUnaligned<long>(capacity - 1);
                            var ival3 = Unsafe.Add<long>(ref vs.UnsafeGetRef<long>(), capacity - 1);
                            var ival4 = Unsafe.Add<long>(ref (bc.Data as DataBlock).RowKeys.UnsafeGetRef<long>(), capacity - 1);
                            var found = bc.TryGetBlock(i, out var c, out var ci);
                            if (!found
                                || !ReferenceEquals(block, c)
                                || i != ci
                            )
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }
            }

            Benchmark.Dump();
            bc.Dispose();
        }
    }
}