using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Diagnostics;

namespace Spreads.Collections.Tests {
    [TestFixture]
    public class BatchMapValuesCursorTests {

        /// <summary>
        /// Very straighforward batch operation for testing
        /// </summary>
        public IReadOnlyOrderedMap<DateTime, double> IncrementMap(IReadOnlyOrderedMap<DateTime, double> batch) {
            var sm = new SortedMap<DateTime, double>();
            foreach (var kvp in batch) {
                sm.Add(kvp.Key, kvp.Value + 1.0);
            }
            return sm;
        }

        public IReadOnlyOrderedMap<DateTime, double> MultiplyMap(IReadOnlyOrderedMap<DateTime, double> batch) {
            var sm = new SortedMap<DateTime, double>();
            foreach (var kvp in batch) {
                sm.Add(kvp.Key, kvp.Value * 10.0);
            }
            return sm;
        }

        [Test]
        public void CouldMoveNextWithoutBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v + 1.0);
            var c = 0;
            while (c < 500 && bmvc.MoveNext()) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }

            while (bmvc.MoveNext(CancellationToken.None).Result) { // Setting IsMutable to false allows us to skip this check: c < 1000 &&
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }
            Assert.AreEqual(count, c);
        }

        [Test]
        public void CouldMovePreviousWithoutBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v + 1.0);
            var c = 0;
            while (c < 500 && bmvc.MoveNext()) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }
            c--;
            while (bmvc.MovePrevious()) {
                c--;
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);

            }
            Assert.AreEqual(0, c);
        }



        [Test]
        public void CouldMoveNextWithBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v + 1.0, this.IncrementMap);
            var c = 0;
            while (c < 500 && bmvc.MoveNext()) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }

            while (bmvc.MoveNext(CancellationToken.None).Result) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }
            Assert.AreEqual(count, c);
        }

        [Test]
        public void CouldMoveNextAsyncWithBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = true; // will mutate after the first batch

            Task.Run(() => {
                Thread.Sleep(1000);
                for (int i = count; i < count * 2; i++) {
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
                    //Thread.Sleep(50);
                }

                sm.IsMutable = false; // stop mutating
                //Console.WriteLine("Set immutable");
            });

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v * 10.0, this.MultiplyMap);
            var c = 0;
            while (c < 5 && bmvc.MoveNext()) {
                Assert.AreEqual(c * 10.0, bmvc.CurrentValue);
                c++;
            }

            while (bmvc.MoveNext(CancellationToken.None).Result) {
                Assert.AreEqual(c * 10.0, bmvc.CurrentValue);
                Console.WriteLine("Value: " + bmvc.CurrentValue);
                c++;
            }
            Assert.AreEqual(2 * count, c);
        }


        [Test]
        public void CouldMovePreviousWithBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v + 1.0, this.IncrementMap);
            var c = 0;
            while (c < 500 && bmvc.MoveNext()) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }
            c--;
            while (bmvc.MovePrevious()) {
                c--;
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);

            }
            Assert.AreEqual(0, c);
        }


        [Test]
        public void CouldMoveAtWithBatching() {
            var sm = new SortedMap<DateTime, double>();

            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;

            var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => v + 1.0, this.IncrementMap);
            var c = 0;
            while (c < 500 && bmvc.MoveNext()) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c++;
            }
            c--;
            while (bmvc.MoveAt(DateTime.UtcNow.Date.AddSeconds(c), Lookup.EQ)) {
                Assert.AreEqual(c + 1.0, bmvc.CurrentValue);
                c--;
            }
            Assert.AreEqual(-1, c);
        }



        public IReadOnlyOrderedMap<DateTime, double> YeppMathProviderSample(IReadOnlyOrderedMap<DateTime, double> batch) {
            var mathProviderImpl = new Spreads.NativeMath.YepppMathProvider();
            IReadOnlyOrderedMap<DateTime, double> sm2;
            var ok = mathProviderImpl.AddBatch(3.1415926, batch, out sm2);
            return sm2;
        }

        [Test]
        public void CouldAddWithYeppMathProvider() {

            var sm = new SortedChunkedMap<DateTime, double>();
            var count = 40000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 10000; rounds++) {
                var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, 
                    (v) => v + 3.1415926, YeppMathProviderSample); //
                while (bmvc.MoveNext()) {
                    sum += bmvc.CurrentValue;
                }
            }
            sw.Stop();

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 10000 * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));
            Console.WriteLine(sum);
            var c = 0;
            //foreach (var kvp in sm2)
            //{
            //    Assert.AreEqual(c + 1, kvp.Value);
            //    c++;
            //}

        }


        public IReadOnlyOrderedMap<DateTime, double> MathProviderSample(IReadOnlyOrderedMap<DateTime, double> batch)
        {
            var mathProviderImpl = new MathProviderImpl() as IVectorMathProvider;
            IReadOnlyOrderedMap<DateTime, double> sm2;
            var ok = mathProviderImpl.AddBatch(3.1415926, batch, out sm2);
            return sm2;
        }

        [Test]
        public void CouldAddWitDefaultMathProvider() {
            var sm = new SortedMap<DateTime, double>(4000);
            var count = 100000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;
            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 1000; rounds++) {
                var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) =>
                {
                    //Thread.SpinWait(50);
                    //var fakeSum = 0;
                    //for (int i = 0; i < 100; i++) {
                    //    fakeSum += i;
                    //}
                    //fakeSum = 0;
                    return v + 3.1415926;
                }, MathProviderSample); //
                //var bmvc = new MapCursor<DateTime, double, double>(sm.GetCursor, (k,v) => Math.Log(v)) as ICursor<DateTime, double>; //
                while (bmvc.MoveNext()){
                    sum += bmvc.CurrentValue;
                }
            }
            sw.Stop();

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000 * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));
            Console.WriteLine(sum);

            var c = 0;
            //foreach (var kvp in sm2) {
            //    Assert.AreEqual(c + 1, kvp.Value);
            //    c++;
            //}

        }


        [Test]
        public void CouldAddWitDefaultMathProviderViaOperator() {
            var sm = new SortedChunkedMap<DateTime, double>(4000);
            var count = 100000;
            OptimizationSettings.AlwaysBatch = true;
            for (int i = 0; i < count; i++) {
                if(i % 99 != 0)
                     sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 1000; rounds++)
            {
                var sm2 = sm + 3.1415926;
                var bmvc = sm2.GetCursor();
                while (bmvc.MoveNext()) {
                    sum += bmvc.CurrentValue;
                }

                //foreach (var kvp in sm2)
                //{
                //    sum += kvp.Value;
                //}
            }
            sw.Stop();

            Console.WriteLine("Elapsed msec: {0}", sw.ElapsedMilliseconds);
            Console.WriteLine("Ops: {0}", Math.Round(0.000001 * count * 1000 * 1000.0 / (sw.ElapsedMilliseconds * 1.0), 2));
            Console.WriteLine(sum);

            var c = 0;
            //foreach (var kvp in sm2) {
            //    Assert.AreEqual(c + 1, kvp.Value);
            //    c++;
            //}

        }



    }
}
