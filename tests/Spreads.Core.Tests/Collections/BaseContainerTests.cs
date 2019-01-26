// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System.Linq;

#pragma warning disable 618

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public unsafe class BaseContainerTests
    {
        [Test]
        public void CouldTryGetChunkAtSingleChunk()
        {
            var capacity = 100;
            var bc = new BaseContainer<int>();
            var chunk = new DataBlockStorage();
            var rm = ArrayMemory<int>.Create(Enumerable.Range(0, capacity).ToArray(), externallyOwned: true);
            var vs = VectorStorage.Create(rm, 0, rm.Length, 1);

            chunk._rowIndex = vs;

            // half the size
            chunk.RowLength = vs.Length / 2;

            bc.DataBlock = chunk;

            var searchIndex = 40;
            var found = bc.TryGetBlockAt(searchIndex, out var c, out var ci);
            Assert.IsTrue(found);
            Assert.AreSame(chunk, c);
            Assert.AreEqual(searchIndex, ci);
        }

        [Test]
        public void CouldTryFindChunkAtSingleChunk()
        {
            var capacity = 100;
            var bc = new BaseContainer<long>();
            var chunk = new DataBlockStorage();
            var rm = ArrayMemory<long>.Create(Enumerable.Range(0, capacity).Select(x => (long)x).ToArray(), externallyOwned: true);
            var vs = VectorStorage.Create(rm, 0, rm.Length, 1);

            chunk._rowIndex = vs;
            // half the size
            chunk.RowLength = vs.Length / 2;

            bc.DataBlock = chunk;

            var searchIndex = 40L;
            var searchIndexRef = searchIndex;
            var found = bc.TryFindBlockAt(ref searchIndexRef, Lookup.LT, out var c, out var ci);
            Assert.IsTrue(found);
            Assert.AreSame(chunk, c);
            Assert.AreEqual(searchIndex - 1, ci);
            Assert.AreEqual(searchIndex - 1, searchIndexRef);
        }

        [Test, Explicit("long running")]
        public void CouldTryFindChunkAtSingleChunkBench()
        {
            var count = 50_000_000;
            var rounds = 20;

            // for this test capacity is irrelevant - interpolation search hits exact position on first try
            var capacity = count / 100;
            var bc = new BaseContainer<int>();
            var chunk = new DataBlockStorage();
            var rm = ArrayMemory<int>.Create(Enumerable.Range(0, capacity).ToArray(), externallyOwned: true);
            var vs = VectorStorage.Create(rm, 0, rm.Length, 1);

            chunk._rowIndex = vs;
            // half the size
            chunk.RowLength = vs.Length;

            bc.DataBlock = chunk;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("TryFindChunkAt", count))
                {
                    var m = count / capacity;
                    for (int _ = 0; _ < m; _++)
                    {
                        for (int i = 1; i < capacity; i++)
                        {
                            var searchIndexRef = i;
                            var found = bc.TryFindBlockAt(ref searchIndexRef, Lookup.LE, out var c, out var ci);
                            if (!found
                                || !ReferenceEquals(chunk, c)
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
        }
    }
}
