// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class MapValuesTests
    {
        [Test]
        public void CouldMapValues()
        {
            var sm = new SortedMap<int, int>
            {
                { 1, 1 }
            };

            var map = new MapValuesSeries<int, int, int, SortedMapCursor<int, int>>(sm, i => i * 2);
            var map1 = new MapValuesSeries<int, int, int, MapValuesSeries<int, int, int, SortedMapCursor<int, int>>>(map, i => i * 2);
            var map2 = new MapValuesSeries<int, int, int>(map.After(0), i => i * 2);
            var map3 = new MapValuesSeries<int, int, int>(map.After(2), i => i * 2);

            Assert.AreEqual(2, map.First.Value);
            Assert.AreEqual(4, map1.First.Value);
            Assert.AreEqual(4, map2.First.Value);
            Assert.True(map3.IsEmpty);
        }

        [Test, Ignore]
        public void CouldMapValuesBenchmark()
        {
            var sm = new SortedMap<int, int>();
            var count = 10000000;
            //var arr = new int[count];
            for (int i = 0; i < count; i++)
            {
                //arr[i] = i;
                sm.AddLast(i, i);
            }
            //var sm = SortedMap<int, int>.OfSortedKeysAndValues(arr, arr);

            for (int r = 0; r < 10; r++)
            {
                var sw = new Stopwatch();
                sw.Restart();
                var map = new MapValuesSeries<int, int, int, SortedMapCursor<int, int>>(sm, i => i * 2);
                long sum = 0;
                foreach (var kvp in map)
                {
                    sum += kvp.Value;
                }
                sw.Stop();
                Assert.IsTrue(sum > 0);

                Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
                Console.WriteLine($"Mops {sw.MOPS(count)}");
            }

            for (int r = 0; r < 10; r++)
            {
                var sw = new Stopwatch();
                sw.Restart();
                var map = sm.Select(x => new KeyValuePair<int, int>(x.Key, x.Value * 2));
                long sum = 0;
                foreach (var kvp in map)
                {
                    sum += kvp.Value;
                }
                sw.Stop();
                Assert.IsTrue(sum > 0);

                Console.WriteLine($"Elapsed LINQ {sw.ElapsedMilliseconds}");
                Console.WriteLine($"Mops {sw.MOPS(count)}");
            }
        }
    }
}