// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Collections.Experimental;
using Spreads.Utils;
using System.Collections.Generic;
using System.Linq;
using Spreads.Buffers;
using Spreads.Collections.Internal;

namespace Spreads.Core.Tests.Collections
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesAppendTests
    {
        [Test]
        public void ArrayOfStrongRefsHasNullPointer()
        {
            var srrm = BufferPool<StrongReference<DataBlock>>.MemoryPool.RentMemory(1024);
            Assert.IsTrue(srrm.Pointer == null);
        }

        
        [Test]
        public void CouldAppendSeries()
        {
            var sa = new AppendSeries<int, int>();

            Assert.IsTrue(sa.TryAddLast(1, 1).Result);
            Assert.IsFalse(sa.TryAddLast(1, 1).Result);

            Assert.IsTrue(sa.TryAddLast(2, 2).Result);

            // TODO uncomment for CI, annoying for debug
            //Assert.Throws<KeyNotFoundException>(() =>
            //{
            //    var _ = sa[0];
            //});

            Assert.AreEqual(1, sa[1]);
            Assert.AreEqual(2, sa[2]);

            Assert.AreEqual(2, sa.Count());

            for (int i = 3; i < 42000; i++)
            {
                Assert.IsTrue(sa.TryAddLast(i, i).Result);
                Assert.AreEqual(i, sa.Last.Present.Value);
            }

            //// TODO remove when implemented
            //Assert.Throws<NotImplementedException>(() =>
            //{
            //    for (int i = 32000; i < 33000; i++)
            //    {
            //        Assert.IsTrue(sa.TryAddLast(i, i).Result);
            //    }
            //});

            sa.Dispose();
        }

        [Test, Explicit("long running")]
        public void CouldAppendSeriesBench()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
            }

            var count = 10_000_000;
            var rounds = 200;

            var sa = new AppendSeries<int, uint>();
            var sl = new SortedList<int, uint>();

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Append", count))
                {
                    for (int i = r * count; i < (r + 1) * count; i++)
                    {
                        var _ = sa.TryAddLastDirect(i, (uint)i);
                        //if (!sa.TryAddLast(i, i).Result)
                        //{
                        //    Assert.Fail("Cannot add " + i);
                        //}
                    }
                }

                Console.WriteLine($"Added {((r + 1) * count / 1000000).ToString("N")}");
            }

            

            //for (int r = 0; r < rounds; r++)
            //{
            //    using (Benchmark.Run("SL.Append", count))
            //    {
            //        for (int i = r * count; i < (r + 1) * count; i++)
            //        {
            //            sl.Add(i, (uint)i);
            //            //if (!sa.TryAddLast(i, i).Result)
            //            //{
            //            //    Assert.Fail("Cannot add " + i);
            //            //}
            //        }
            //    }
            //    Console.WriteLine($"Added {((r + 1) * count / 1000000).ToString("N")}");
            //}


            // sa.Dispose();
            Benchmark.Dump();
        }
    }
}
