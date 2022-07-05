// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using Spreads.Collections.Generic;

namespace Spreads.Core.Tests.Collections
{
    // TODO Add CI, make tests fast in debug, long benchmarks explicit
    [TestFixture]
    public class SortedDequeTests
    {
        private bool DequeIsSorted<T>(SortedDeque<T> deque)
        {
            bool isFirst = true;
            T prev = default;
            foreach (var e in deque)
            {
                if (isFirst)
                {
                    isFirst = false;
                    prev = e;
                }
                else
                {
                    if (deque._comparer.Compare(e, prev) < 0) return false;
                    prev = e;
                }
            }
            return true;
        }

        [Test]
        public void CouldRemoveFirstAddInTheMiddle()
        {
            var sd = new SortedDeque<int>();
            sd.Add(1);
            sd.Add(3);
            sd.Add(5);
            sd.Add(7);

            sd.First.ShouldBe(1);
            sd.Last.ShouldBe(7);

            var fst = sd.RemoveFirst();
            sd.First.ShouldBe(3);
            sd.Add(4);
            sd.ToList().IndexOf(4).ShouldBe(1);
            sd.ToList().IndexOf(5).ShouldBe(2);
            sd.ToList().IndexOf(7).ShouldBe(3);

            var last = sd.RemoveLast();
            sd.Add(8);
            sd.ToList().IndexOf(4).ShouldBe(1);
            sd.ToList().IndexOf(5).ShouldBe(2);
            sd.ToList().IndexOf(8).ShouldBe(3);

            sd.Add(6);
            sd.ToList().IndexOf(4).ShouldBe(1);
            sd.ToList().IndexOf(5).ShouldBe(2);
            sd.ToList().IndexOf(6).ShouldBe(3);
            sd.ToList().IndexOf(8).ShouldBe(4);
        }

        [Test]
        public void CouldAddBeyondInitialCapacity()
        {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++)
            {
                sd.Add(i);
            }

            for (int i = 0; i < 4; i++)
            {
                sd.ToList().IndexOf(i).ShouldBe(i);
            }
        }

        [Test]
        public void CouldAddRemoveWithFixedSize()
        {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++)
            {
                sd.Add(i);
            }

            for (int i = 4; i < 100; i++)
            {
                sd.RemoveFirst();
                sd.Add(i);
                sd.First.ShouldBe(i - 3);
                sd.Last.ShouldBe(i);
            }
        }

        [Test]
        public void CouldAddRemoveIncreasing()
        {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++)
            {
                sd.Add(i);
            }

            for (int i = 4; i < 100; i++)
            {
                if (i % 2 == 0) sd.RemoveFirst();
                sd.Add(i);
                //Assert.AreEqual(i - 3, sd.First);
                sd.Last.ShouldBe(i);
            }
        }

        [Test, Explicit("long running")]
        public void AddRemoveTest()
        {
            var rng = new System.Random();
            for (int r = 0; r < 1000; r++)
            {
                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 50; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }

                for (int i = 0; i < 1000; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }

                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());

                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());
                    DequeIsSorted(sd).ShouldBe(true);
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd)
                    {
                        if (c == 0)
                        {
                            c++;
                            prev = e;
                        }
                        else
                        {
                            (e > prev).ShouldBe(true);
                            prev = e;
                        }
                    }
                }
            }
        }

        [Test]
        public void AddRemoveTestWithRemoveElement()
        {
            var rng = new System.Random();
            for (int r = 0; r < 1000; r++)
            {
                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 50; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }

                for (int i = 0; i < 1000; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }

                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());

                    var first = sd.First;
                    sd.Remove(first);
                    set.Remove(first);
                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());
                    DequeIsSorted(sd).ShouldBe(true);
                }
            }
        }

        [Test, Explicit("long running")]
        public void RemoveTest()
        {
            for (int r = 0; r < 10000; r++)
            {
                var rng = new Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }
                while (sd.Count > 0)
                {
                    var first = sd.RemoveFirst();
                    set.Remove(first);
                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());

                    DequeIsSorted(sd).ShouldBe(true);

                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd)
                    {
                        if (c == 0)
                        {
                            c++;
                            prev = e;
                        }
                        else
                        {
                            (e > prev).ShouldBe(true);
                            prev = e;
                        }
                    }
                }
            }
        }

        [Test]
        public void CouldRemoveInTheMiddle()
        {
            for (int r = 0; r < 1000; r++)
            {
                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                }
                while (sd.Count > 0)
                {
                    var midElement = sd._buffer[((sd._firstOffset + sd._count / 2) % sd._buffer.Length)];
                    sd.Remove(midElement);
                    set.Remove(midElement);
                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());
                    DequeIsSorted(sd).ShouldBe(true);
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd)
                    {
                        if (c == 0)
                        {
                            c++;
                            prev = e;
                        }
                        else
                        {
                            (e > prev).ShouldBe(true);
                            prev = e;
                        }
                    }
                }
            }
        }

        [Test]
        public void CouldRemoveInTheMiddleSplit()
        {
            var rng = new System.Random();

            var sd = new SortedDeque<int>();
            var set = new HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                if (i % 2 == 0)
                {
                    set.Add(i);
                    sd.TryAdd(i);
                }
                else
                {
                    set.Add(-i);
                    sd.TryAdd(-i);
                }
            }
            {
                var midElement = sd._buffer[0]; //[.___    ____]
                sd.Remove(midElement);
                set.Remove(midElement);
                sd.Count.ShouldBe(set.Count);
                sd.Sum().ShouldBe(set.Sum());
                DequeIsSorted(sd).ShouldBe(true);
                var c = 0;
                var prev = 0;
                foreach (var e in sd)
                {
                    if (c == 0)
                    {
                        c++;
                        prev = e;
                    }
                    else
                    {
                        (e > prev).ShouldBe(true);
                        prev = e;
                    }
                }
            }
            {
                var midElement = sd._buffer[sd._buffer.Length - 1]; //[___    ____.]
                sd.Remove(midElement);
                set.Remove(midElement);
                sd.Count.ShouldBe(set.Count);
                sd.Sum().ShouldBe(set.Sum());
                DequeIsSorted(sd).ShouldBe(true);
                var c = 0;
                var prev = 0;
                foreach (var e in sd)
                {
                    if (c == 0)
                    {
                        c++;
                        prev = e;
                    }
                    else
                    {
                        (e > prev).ShouldBe(true);
                        prev = e;
                    }
                }
            }
        }

        [Test, Explicit("long running")]
        public void AddTest()
        {
            for (int r = 0; r < 10000; r++)
            {
                var rng = new System.Random();

                var sd = new SortedDeque<int>();
                var set = new HashSet<int>();
                for (int i = 0; i < 100; i++)
                {
                    for (var next = rng.Next(1000); !set.Contains(next);)
                    {
                        set.Add(next);
                        sd.TryAdd(next);
                    }
                    sd.Count.ShouldBe(set.Count);
                    sd.Sum().ShouldBe(set.Sum());
                    DequeIsSorted(sd).ShouldBe(true);
                    var c = 0;
                    var prev = 0;
                    foreach (var e in sd)
                    {
                        if (c == 0)
                        {
                            c++;
                            prev = e;
                        }
                        else
                        {
                            (e > prev).ShouldBe(true);
                            prev = e;
                        }
                    }
                }
            }
        }

        [Test]
        public void AddSequentialTest()
        {
            var sd = new SortedDeque<int>();
            for (var r = 0; r < 1000; r++)
            {
                sd.TryAdd(r);
            }
        }

        [Test]
        public void AddReverseSequentialTest()
        {
            var sd = new SortedDeque<int>();
            for (var r = 1000; r >= 0; r--)
            {
                sd.TryAdd(r);
            }
        }

        [Test]
        public void AddDatesTest()
        {
            var sd = new SortedDeque<DateTime>();
            DateTime dt;

            dt = DateTime.Parse("10 / 2 / 2015 10:29:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 2:06:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 1 / 2015 11:30:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 10:30:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 1 / 2015 11:31:00 PM");
            sd.Add(dt);
            dt = DateTime.Parse("10 / 2 / 2015 2:07:00 PM");
            sd.Add(dt);
            sd.Count.ShouldBe(6);
        }

        [Test, Explicit("long running")]
        public void CouldCompareDatesManyTimes()
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            var now = DateTime.UtcNow;
            var later = DateTime.UtcNow.AddSeconds(1.0);
            for (int i = 0; i < 100000000; i++)
            {
                if (now > later)
                {
                    throw new ApplicationException("no way");
                }
                //later = later.AddTicks(1);
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [Test]
        public void CouldAddKVsWithSimilarKey()
        {
            var sd = new SortedDeque<int, int>(2, new KVPComparer<int, int>(KeyComparer<int>.Default, KeyComparer<int>.Default));
            for (int i = 0; i < 3; i++)
            {
                sd.Add(new KeyValuePair<int, int>(1, i));
            }
            var expected = 1 * 3;
            int[] values = new int[3];
            foreach (var item in sd)
            {
                values[item.Value] = item.Key;
            }
            var actual = values.Sum();
            Console.WriteLine(actual);
            actual.ShouldBe(expected);
        }
    }
}
