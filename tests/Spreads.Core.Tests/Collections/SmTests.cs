// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Collections
{
    // TODO Move to Collections.Tests project
    [TestFixture]
    public class SMTests
    {
        [Test, Explicit("long running")]
        public void AddSpeed()
        {
            const int count = 1_000_000;
            for (int r = 0; r < 10; r++)
            {
                var sl = new SortedList<int, int>();
                var sm = new SortedMap<int, int>();
                var scm = new SortedChunkedMap<int, int>();

                var c = sm.GetCursor();

                sm._isSynchronized = false;
                scm._isSynchronized = false;

                //using (Benchmark.Run("SL", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        if (i != 2)
                //        {
                //            sl.Add(i, i);
                //        }
                //    }
                //}

                //using (Benchmark.Run("SM", count))
                //{
                //    for (int i = 0; i < count; i++)
                //    {
                //        if (i != 2)
                //        {
                //            sm.Add(i, i);
                //        }
                //    }
                //}

                using (Benchmark.Run("SCM", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (i != 2)
                        {
                            scm.Add(i, i);
                        }
                    }
                }
                scm.Dispose();
            }

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void EnumerateScmSpeed()
        {
            const int count = 10_000_000;

            var sl = new SortedList<int, int>();
            var sm = new SortedMap<int, int>();
            var scm = new SortedChunkedMap<int, int>();

            for (int i = 0; i < count; i++)
            {
                if (i % 1000 != 0)
                {
                    sl.Add(i, i);
                    sm.Add(i, i);
                    //scm.Add(i, i);
                }
            }

            sm.Complete();
            // scm.Complete();

            for (int r = 0; r < 20; r++)
            {
                //var sum1 = 0L;
                //using (Benchmark.Run("SL", count))
                //{
                //    using (var c = sl.GetEnumerator())
                //    {
                //        while (c.MoveNext())
                //        {
                //            sum1 += c.Current.Value;
                //        }
                //    }
                //}
                //Assert.True(sum1 > 0);

                var sum2 = 0L;
                using (Benchmark.Run("SM Current.Value", count))
                {
                    using (var c = sm.GetEnumerator())
                    {
                        while (c.MoveNext())
                        {
                            sum2 += c.Current.Value;
                        }
                    }
                }
                //Assert.AreEqual(sum1, sum2);

                var sum3 = 0L;
                using (Benchmark.Run("SM CurrentValue", count))
                {
                    using (var c = sm.GetEnumerator())
                    {
                        while (c.MoveNext())
                        {
                            sum3 += c.CurrentValue;
                        }
                    }
                }
                //Assert.AreEqual(sum1, sum3);

                //var sum4 = 0L;
                //using (Benchmark.Run("SCM Current.Value", count))
                //{
                //    using (var c = scm.GetEnumerator())
                //    {
                //        while (c.MoveNext())
                //        {
                //            sum4 += c.Current.Value;
                //        }
                //    }
                //}
                //Assert.AreEqual(sum1, sum4);

                //var sum5 = 0L;
                //using (Benchmark.Run("SCM CurrentValue", count))
                //{
                //    using (var c = scm.GetEnumerator())
                //    {
                //        while (c.MoveNext())
                //        {
                //            sum5 += c.CurrentValue;
                //        }
                //    }
                //}
                //Assert.AreEqual(sum1, sum5);
            }

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void TGVSpeed()
        {
            for (int size = 0; size < 3; size++)
            {
                var count = (int)(1024 * Math.Pow(2, size));
                const int mult = 1000;
                var sl = new SortedList<DateTime, int>();
                var sm = new SortedMap<DateTime, int>(count);
                var scm = new SortedChunkedMap<DateTime, int>();

                var start = DateTime.Today.ToUniversalTime();
                for (int i = 0; i < count; i++)
                {
                    if (i != 2) // make irregular
                    {
                        sl.Add(start.AddTicks(i), i);
                        sm.Add(start.AddTicks(i), i);
                        scm.Add(start.AddTicks(i), i);
                    }
                }

                Assert.IsFalse(sm.IsCompleted);
                Assert.IsFalse(sm.IsCompleted);
                Assert.IsTrue(sm.IsSynchronized);
                sm.Complete();
                Assert.IsTrue(sm.IsCompleted);
                Assert.IsTrue(sm.IsCompleted);
                Assert.IsFalse(sm.IsSynchronized);
                scm.Complete();

                for (int r = 0; r < 20; r++)
                {
                    //var sum1 = 0L;
                    //using (Benchmark.Run("SL", count * mult, true))
                    //{
                    //    for (int j = 0; j < mult; j++)
                    //    {
                    //        for (int i = 0; i < count; i++)
                    //        {
                    //            if (sl.TryGetValue(start.AddTicks(i), out var v))
                    //            {
                    //                sum1 += v;
                    //            }
                    //        }
                    //    }

                    //}
                    //Assert.True(sum1 > 0);

                    var sum2 = 0L;
                    using (Benchmark.Run("SM", count * mult, true))
                    {
                        for (int j = 0; j < mult; j++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (sm.TryGetValueUnchecked(start.AddTicks(i), out var v))
                                {
                                    sum2 += v;
                                }
                            }
                        }
                    }
                    //Assert.True(sum2 > 0);
                    //Assert.AreEqual(sum1, sum2);

                    //var sum3 = 0L;
                    //using (Benchmark.Run("SCM", count * mult, true))
                    //{
                    //    for (int j = 0; j < mult; j++)
                    //    {
                    //        for (int i = 0; i < count; i++)
                    //        {
                    //            if (scm.TryGetValue(start.AddTicks(i), out var v))
                    //            {
                    //                sum3 += v;
                    //            }
                    //        }
                    //    }
                    //}
                    //Assert.True(sum3 > 0);
                    //Assert.AreEqual(sum2, sum3);
                }

                Benchmark.Dump($"Size = {Math.Pow(2, size)}k elements");
            }
        }

        [Test, Explicit("long running")]
        public void BinarySearchSpeed()
        {
            for (int size = 0; size < 3; size++)
            {
                var count = (int)(1024 * Math.Pow(2, size));
                const int mult = 1000;
                var arr = new DateTime[count];

                var start = DateTime.Today.ToUniversalTime();
                for (int i = 0; i < count; i++)
                {
                    arr[i] = start.AddTicks(i);
                }

                for (int r = 0; r < 20; r++)
                {
                    var cmp1 = Comparer<DateTime>.Default;
                    var sum1 = 0L;
                    using (Benchmark.Run("Array.BS", count * mult, false))
                    {
                        for (int j = 0; j < mult; j++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                sum1 += Array.BinarySearch(arr, 0, arr.Length, start.AddTicks(i), cmp1);
                            }
                        }
                    }
                    Assert.True(sum1 > 0);

                    var sum2 = 0L;
                    var cmp2 = KeyComparer<DateTime>.Default;
                    using (Benchmark.Run("KeyComparer.BS", count * mult, false))
                    {
                        for (int j = 0; j < mult; j++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                sum2 += cmp2.BinarySearch(arr, 0, arr.Length, start.AddTicks(i));
                            }
                        }
                    }
                    Assert.True(sum2 > 0);

                    Assert.AreEqual(sum1, sum2);
                }

                Benchmark.Dump($"Size = {Math.Pow(2, size)}k elements");
            }
        }

        [Test, Explicit("long running")]
        public async Task MNATest()
        {
            var sm = new SortedMap<int, int>();
            var count = 1_0;
            var sum = 0;
            using (Benchmark.Run("MNA"))
            {
                var _ = Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        sm.TryAddLast(i, i);
                    }
                    sm.Complete();
                });

                var c = sm.GetCursor();
                while (await c.MoveNextAsync())
                {
                    sum += c.CurrentValue;
                }
            }
            Assert.IsTrue(sum > 0);

            Benchmark.Dump();
        }
    }
}