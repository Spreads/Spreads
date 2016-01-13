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
using System.Numerics;
using Spreads;
using Microsoft.FSharp.Core;

namespace Spreads.Collections.Tests.Cursors {
    public class SimdMathProvider : IVectorMathProvider {

        public bool AddBatch<K>(IReadOnlyOrderedMap<K, double> left, IReadOnlyOrderedMap<K, double> right, out IReadOnlyOrderedMap<K, double> value) {
            throw new NotImplementedException();
        }

        public bool AddBatch<K>(double scalar, IReadOnlyOrderedMap<K, double> batch, out IReadOnlyOrderedMap<K, double> value) {
            var sm = batch as SortedMap<K, double>;
            if (!ReferenceEquals(sm, null)) {
                double[] newValues = OptimizationSettings.ArrayPool.TakeBuffer<double>(sm.size);
                double[] buffer = new double[Vector<double>.Count];
                for (int c = 0; c < Vector<double>.Count; c++) {
                    buffer[c] = scalar;
                }
                var tempVector = new System.Numerics.Vector<double>(buffer);
                int i;
                for (i = 0; i < newValues.Length; i = i + Vector<double>.Count) {
                    var vec = new Vector<double>(sm.values, i);
                    vec = Vector.Add(vec, tempVector);
                    vec.CopyTo(newValues, i);
                }
                for (; i < newValues.Length; i++) {
                    newValues[i] = sm.values[i] + scalar;
                }

                var newKeys = sm.IsMutable ? sm.keys.ToArray() : sm.keys;
                var newSm = SortedMap<K, double>.OfSortedKeysAndValues(newKeys, newValues, sm.size, sm.Comparer, false, sm.IsRegular);
                value = newSm;
                return true;
            }
            throw new NotImplementedException();
        }

        public bool SumBatch<K>(double scalar, IReadOnlyOrderedMap<K, double> batch, out double value) {
            var sm = batch as SortedMap<K, double>;
            if (!ReferenceEquals(sm, null)) {
                double[] newValues = new double[sm.size];
                //Yeppp.Core.Add_V64fS64f_V64f(sm.values, 0, scalar, newValues, 0, sm.size);
                value = Yeppp.Core.Sum_V64f_S64f(sm.values, 0, sm.size);
                return true;
            }
            throw new NotImplementedException();
        }

        public void AddVectors<T>(T[] x, T[] y, T[] result) {
            throw new NotImplementedException();
        }

        public bool MapBatch<K, V, V2>(FSharpFunc<V, V2> mapF, IReadOnlyOrderedMap<K, V> batch, out IReadOnlyOrderedMap<K, V2> value) {
            throw new NotImplementedException();
        }
    }


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


        public IReadOnlyOrderedMap<DateTime, double> SimdMathProviderSample(IReadOnlyOrderedMap<DateTime, double> batch) {
            var mathProviderImpl = new SimdMathProvider();
            IReadOnlyOrderedMap<DateTime, double> sm2;
            var ok = mathProviderImpl.AddBatch(3.1415926, batch, out sm2);
            return sm2;
        }

        [Test]
        public void CouldAddWithYeppMathProvider() {

            var sm = new SortedChunkedMap<DateTime, double>();
            var count = 1000;

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

        [Test]
        public void CouldAddWithSimdMathProvider() {

            var sm = new SortedChunkedMap<DateTime, double>();
            var count = 1000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 10000; rounds++) {
                var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor,
                    (v) => v + 3.1415926, SimdMathProviderSample); //
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


        public IReadOnlyOrderedMap<DateTime, double> MathProviderSample(IReadOnlyOrderedMap<DateTime, double> batch) {
            var mathProviderImpl = new MathProviderImpl() as IVectorMathProvider;
            IReadOnlyOrderedMap<DateTime, double> sm2;
            var ok = mathProviderImpl.AddBatch(3.1415926, batch, out sm2);
            return sm2;
        }

        [Test]
        public void CouldAddWitDefaultMathProvider() {
            var sm = new SortedMap<DateTime, double>(4000);
            var count = 10000;

            for (int i = 0; i < count; i++) {
                sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }
            sm.IsMutable = false;
            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 1000; rounds++) {
                var bmvc = new BatchMapValuesCursor<DateTime, double, double>(sm.GetCursor, (v) => {
                    //Thread.SpinWait(50);
                    //var fakeSum = 0;
                    //for (int i = 0; i < 100; i++) {
                    //    fakeSum += i;
                    //}
                    //fakeSum = 0;
                    return v + 3.1415926;
                }, MathProviderSample); //
                //var bmvc = new MapCursor<DateTime, double, double>(sm.GetCursor, (k,v) => Math.Log(v)) as ICursor<DateTime, double>; //
                while (bmvc.MoveNext()) {
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
            var count = 10000;
            OptimizationSettings.AlwaysBatch = true;
            for (int i = 0; i < count; i++) {
                if (i % 99 != 0)
                    sm.Add(DateTime.UtcNow.Date.AddSeconds(i), i);
            }

            var sw = new Stopwatch();
            sw.Start();
            var sum = 0.0;
            for (int rounds = 0; rounds < 1000; rounds++) {
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
