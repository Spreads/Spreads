// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Utils;
using System.Linq;

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public unsafe class BlockCursorTests
    {
        private static BaseContainer<int> CreateIntBaseContainer(int capacity, int length)
        {
            var bc = new BaseContainer<int>();
            var block = new DataBlock();
            var rm = ArrayMemory<int>.Create(Enumerable.Range(0, capacity).ToArray(), externallyOwned: true);
            var vs = VectorStorage.Create(rm, 0, rm.Length, 1);
            block._rowIndex = vs;
            block.RowLength = length;
            bc.DataBlock = block;
            return bc;
        }

        [Test]
        public void CouldMoveNextX()
        {
            var count = 100;
            var len = count / 2;
            var bc = CreateIntBaseContainer(count, len);
            var c = new BlockCursor<int>(bc);

            for (int i = 0; i < len; i++)
            {
                Assert.IsTrue(c.MoveNext());
                Assert.AreEqual(i, c._blockPosition);
            }

            Assert.AreEqual(1 - len, c.Move(long.MinValue, true));

        }

        [Test, Explicit("long running")]
        public void CouldMoveNextBench()
        {
            var count = 100000;
            var rounds = 50;
            var mult = 1000;

            var bc = CreateIntBaseContainer(count, count);
            
            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("MoveNext", count * mult))
                {
                    for (int _ = 0; _ < mult; _++)
                    {
                        var c = new BlockCursor<int>(bc);
                        for (int i = 0; i < count; i++)
                        {
                            c.MoveNext();
                            
                            //if (i != c._blockPosition)
                            //{
                            //    // Assert.Fail("i != c._blockPosition");
                            //}

                            var v = c.CurrentKey;
                            //if (i != v)
                            //{
                            //    Assert.Fail("i != v");
                            //}
                        }
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}
