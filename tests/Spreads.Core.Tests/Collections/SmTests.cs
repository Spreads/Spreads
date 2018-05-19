// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;
using System.Collections.Generic;

namespace Spreads.Core.Tests.Collections
{
    // TODO Move to Collections.Tests project
    [TestFixture]
    public class SMTests
    {
        [Test, Ignore("long running")]
        public void EnumerateScmSpeed()
        {
            const int count = 10_000_000;

            var sl = new SortedList<int, int>();
            var sm = new SortedMap<int, int>();
            // var scm = new SortedMap<int, int>();

            for (int i = 0; i < count; i++)
            {
                if (i % 1000 != 0)
                {
                    sl.Add(i, i);
                    sm.Add(i, i);
                    // scm.Add(i, i);
                }
            }

            //var ism = new ImmutableSortedMap<int, int>(sm);

            long sum;

            for (int r = 0; r < 20; r++)
            {
                sum = 0L;
                using (Benchmark.Run("SL", count))
                {
                    using (var c = sl.GetEnumerator())
                    {
                        while (c.MoveNext())
                        {
                            sum += c.Current.Value;
                        }
                    }
                }
                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("SM", count))
                {
                    using (var c = sm.GetEnumerator())
                    {
                        while (c.MoveNext())
                        {
                            sum += c.Current.Value;
                        }
                    }
                }
                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("ISM", count))
                {
                    foreach (var item in sm)
                    {
                        sum += item.Value;
                    }
                }
                Assert.True(sum > 0);

                //sum = 0L;
                //using (Benchmark.Run("SCM", count))
                //{
                //    using (var c = scm.GetEnumerator())
                //    {
                //        while (c.MoveNext())
                //        {
                //            sum += c.Current.Value;
                //        }
                //    }
                //}
                //Assert.True(sum > 0);
            }

            Benchmark.Dump();
        }


        [Test, Explicit("long running")]
        public void TGVSpeed()
        {
            for (int size = 0; size < 5; size++)
            {
                var count = (int)(1024 * Math.Pow(2, size));
                const int mult = 1000;
                var sl = new SortedList<DateTime, int>();
                var sm = new SortedMap<DateTime, int>();

                var start = DateTime.Today.ToUniversalTime();
                for (int i = 0; i < count; i++)
                {
                    if (i != 2) // make irregular
                    {
                        sl.Add(start.AddTicks(i), i);
                        sm.Add(start.AddTicks(i), i);
                    }
                }

                Assert.IsFalse(sm.isReadOnly);
                Assert.IsFalse(sm.IsCompleted);
                Assert.IsTrue(sm.IsSynchronized);
                sm.Complete();
                Assert.IsTrue(sm.isReadOnly);
                Assert.IsTrue(sm.IsCompleted);
                Assert.IsFalse(sm.IsSynchronized);

                for (int r = 0; r < 20; r++)
                {
                    var sum1 = 0L;
                    using (Benchmark.Run("SL", count * mult, true))
                    {
                        for (int j = 0; j < mult; j++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (sl.TryGetValue(start.AddTicks(i), out var v))
                                {
                                    sum1 += v;
                                }
                            }
                        }

                    }
                    Assert.True(sum1 > 0);

                    var sum2 = 0L;
                    using (Benchmark.Run("SM", count * mult, true))
                    {
                        for (int j = 0; j < mult; j++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                if (sm.TryGetValue(start.AddTicks(i), out var v))
                                {
                                    sum2 += v;
                                }
                            }
                        }
                    }
                    Assert.True(sum2 > 0);

                    Assert.AreEqual(sum1, sum2);
                }

                Benchmark.Dump($"Size = {Math.Pow(2, size)}k elements");
            }
        }
    }
}