// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Spreads.DataTypes;

namespace Spreads.Tests.Enumerators
{
    [TestFixture]
    public class TimeSliceTests
    {
        [Test]
        public void CouldAggregate()
        {
            var count = 10000;
            var start = DateTime.UtcNow.Date;
            var source = new List<Tick>();
            for (int i = 0; i < count; i++)
            {
                source.Add(new Tick(start.AddMilliseconds((i / 5) * 5), new Price((double)i), i));
            }

            var seconds = source.Select(x => new KeyValuePair<DateTime, Tick>(x.DateTimeUtc, x)).TimeSlice((t) => new OHLCV(t.Price, t.Price, t.Price, t.Price, t.Volume),
                (st, t) =>
                {
                    var res = new OHLCV(
                        st.Open,
                        t.Price > st.High ? t.Price : st.High,
                        t.Price < st.Low ? t.Price : st.Low,
                        t.Price,
                        st.Volume + t.Volume
                        );
                    return res;
                }, UnitPeriod.Second, 1, 0); //.ToSortedMap();

            var grouped =
                source.GroupBy(t => new DateTime((t.DateTimeUtc.Ticks / TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond, t.DateTimeUtc.Kind))
                .Select(gr => gr.Aggregate(new KeyValuePair<DateTime, OHLCV>(default(DateTime), new OHLCV((decimal)(-1), -1, -1, -1, 0, 5)), (st, t) =>
                {
                    if (st.Value.Open == -1)
                    {
                        var res = new OHLCV(
                               t.Price,
                               t.Price,
                               t.Price,
                               t.Price,
                               t.Volume
                           );
                        return new KeyValuePair<DateTime, OHLCV>(gr.Key, res);
                    }
                    else
                    {
                        var res = new OHLCV(
                            st.Value.Open,
                            t.Price > st.Value.High ? t.Price : st.Value.High,
                            t.Price < st.Value.Low ? t.Price : st.Value.Low,
                            t.Price,
                            st.Value.Volume + t.Volume
                        );
                        return new KeyValuePair<DateTime, OHLCV>(gr.Key, res);
                    }
                }));

            var sw = new Stopwatch();
            Console.WriteLine($"Memory: {GC.GetTotalMemory(false)}");
            for (int r = 0; r < 1; r++)
            {
                //Console.WriteLine("Spreads");
                sw.Restart();
                var total = 0L;
                foreach (var kvp in seconds)
                {
                    total += kvp.Value.Volume;
                    Console.WriteLine($"{kvp.Key} - {kvp.Value.Open} - {kvp.Value.High} - {kvp.Value.Low} - {kvp.Value.Close} - {kvp.Value.Volume}");
                }
                if (total == short.MaxValue) Console.WriteLine("avoid optimizations");
                sw.Stop();
                Console.WriteLine($"Spreads: {sw.ElapsedMilliseconds}");

                Console.WriteLine("LINQ");
                sw.Restart();
                var total2 = 0L;
                foreach (var kvp in grouped)
                {
                    total2 += kvp.Value.Volume;
                    Console.WriteLine($"{kvp.Key} - {kvp.Value.Open} - {kvp.Value.High} - {kvp.Value.Low} - {kvp.Value.Close} - {kvp.Value.Volume}");
                }
                if (total2 == short.MaxValue) Console.WriteLine("avoid optimizations");
                sw.Stop();
                Console.WriteLine($"LINQ: {sw.ElapsedMilliseconds}");
                Assert.AreEqual(total, total2);
                Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
            }
            Console.WriteLine($"Memory: {GC.GetTotalMemory(false)}");
            Console.WriteLine($"GC: {GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
        }

        [Test, Explicit("long running")]
        public void CouldAggregateWithHoles()
        {
            var count = 1000000;
            var start = DateTime.UtcNow.Date;
            var source = new List<Tick>();
            source.Add(new Tick(start.AddMilliseconds(250), new Price(1.0), 1));
            source.Add(new Tick(start.AddMilliseconds(750), new Price(2.0), 2));
            source.Add(new Tick(start.AddMilliseconds(2500), new Price(3.0), 3));

            var seconds = source.Select(x => new KeyValuePair<DateTime, Tick>(x.DateTimeUtc, x)).TimeSlice((t) => new OHLCV(t.Price, t.Price, t.Price, t.Price, t.Volume),
                (st, t) =>
                {
                    var res = new OHLCV(
                        st.Open,
                        t.Price > st.High ? t.Price : st.High,
                        t.Price < st.Low ? t.Price : st.Low,
                        t.Price,
                        st.Volume + t.Volume
                        );
                    return res;
                }, UnitPeriod.Second, 1, 0); //.ToSortedMap();

            var sw = new Stopwatch();
            //Console.WriteLine("Spreads");
            sw.Restart();
            var total = 0L;
            foreach (var kvp in seconds)
            {
                total += kvp.Value.Volume;
                Console.WriteLine($"{kvp.Key} - {kvp.Value.Open} - {kvp.Value.High} - {kvp.Value.Low} - {kvp.Value.Close} - {kvp.Value.Volume}");
            }
            if (total == short.MaxValue) Console.WriteLine("avoid optimizations");
        }
    }
}