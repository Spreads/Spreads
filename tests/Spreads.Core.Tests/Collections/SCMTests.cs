//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.

//using NUnit.Framework;
//using Spreads.Collections;
//using Spreads.Utils;
//using System.Collections.Generic;

//namespace Spreads.Core.Tests.Collections
//{
//    // TODO Move to Collections.Tests project
//    [TestFixture]
//    public class SCMTests
//    {
//        [Test, Ignore("long running")]
//        public void EnumerateScmSpeed()
//        {
//            const int count = 10_000_000;

//            var sl = new SortedList<int, int>();
//            var sm = new SortedMap<int, int>();
//            sm.IsSynchronized = false;
//            var scm = new SortedChunkedMap<int, int>();
//            scm.IsSynchronized = false;

//            for (int i = 0; i < count; i++)
//            {
//                if (i % 1000 != 0)
//                {
//                    sl.Add(i, i);
//                    sm.Add(i, i);
//                    scm.Add(i, i);
//                }
//            }

//            //var ism = new ImmutableSortedMap<int, int>(sm);

//            long sum;

//            for (int r = 0; r < 20; r++)
//            {
//                sum = 0L;
//                using (Benchmark.Run("SL", count))
//                {
//                    using (var c = sl.GetEnumerator())
//                    {
//                        while (c.MoveNext())
//                        {
//                            sum += c.Current.Value;
//                        }
//                    }
//                }
//                Assert.True(sum > 0);

//                sum = 0L;
//                using (Benchmark.Run("SM", count))
//                {
//                    using (var c = sm.GetEnumerator())
//                    {
//                        while (c.MoveNext())
//                        {
//                            sum += c.Current.Value;
//                        }
//                    }
//                }
//                Assert.True(sum > 0);

//                //sum = 0L;
//                //using (Benchmark.Run("ISM", count))
//                //{
//                //    foreach (var item in ism)
//                //    {
//                //        sum += item.Value;
//                //    }
//                //}
//                //Assert.True(sum > 0);

//                sum = 0L;
//                using (Benchmark.Run("SCM", count))
//                {
//                    using (var c = scm.GetEnumerator())
//                    {
//                        while (c.MoveNext())
//                        {
//                            sum += c.Current.Value;
//                        }
//                    }
//                }
//                Assert.True(sum > 0);
//            }

//            Benchmark.Dump();
//        }
//    }
//}