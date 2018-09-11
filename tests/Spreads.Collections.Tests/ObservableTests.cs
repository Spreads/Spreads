//// This Source Code Form is subject to the terms of the Mozilla Public
//// License, v. 2.0. If a copy of the MPL was not distributed with this
//// file, You can obtain one at http://mozilla.org/MPL/2.0/.


//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using NUnit.Framework;
//using System.Runtime.InteropServices;
//using System.Threading.Tasks;
//using Spreads.Collections;
//using System.Threading;

//namespace Spreads.Collections.Tests {

//    [TestFixture]
//    public class ObservableTests {

//        public class SumValuesObserver : IObserver<KeyValuePair<int, int>> {
//            private readonly bool _silent;
//            private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

//            public SumValuesObserver(bool silent = false) {
//                _silent = silent;
//            }

//            public int Sum { get; set; } = 0;
//            public bool IsCompleted { get; set; }
//            public Exception Exception { get; set; }
//            public void OnCompleted() {
//                if (!_silent) Console.WriteLine("Completed");
//                _tcs.TrySetResult(true);
//                IsCompleted = true;
//            }

//            public void OnError(Exception error) {
//                if (!_silent) Console.WriteLine(error.Message);
//                _tcs.TrySetResult(false);
//                Exception = error;
//            }

//            public void OnNext(KeyValuePair<int, int> value) {
//                if (!_silent) Console.WriteLine($"Next value: {value.Value}");
//                Sum += value.Value;
//            }

//            public Task<bool> Completed => _tcs.Task;
//        }

//        [Test]
//        public void CouldCompleteObserver() {
//            CouldCompleteObserver(new SortedMap<int, int>());
//            CouldCompleteObserver(new SortedChunkedMap<int, int>());
//            // TODO
//            Trace.TraceWarning("TODO! Indexed map should support zip when keys are equal, e.g. series + series. This is often needed for rows of panel.");
//            //CouldCompleteObserver(new IndexedMap<int, int>());
//        }

//        public void CouldCompleteObserver(IMutableSeries<int, int> map) {
//            var subscriber = new SumValuesObserver();
//            var series = map as Series<int, int>;
//            var cursor = (series + series).GetCursor();
//            cursor.Source.Subscribe(subscriber);
//            var expectedSum = 0;
//            for (int i = 0; i < 10; i++) {
//                map.Add(i, i);
//                expectedSum += i;
//            }
           
//            Assert.IsFalse(subscriber.IsCompleted);
//            map.Complete();
//            Thread.Sleep(100);
//            Assert.IsTrue(subscriber.IsCompleted);
//            Thread.Sleep(1000);
//            Assert.AreEqual(expectedSum * 2, subscriber.Sum);
//        }

//        [Test]
//        public void CouldCompleteObserverBenchmark() {
//            var count = 10000000;
//            var map = new SortedMap<int, int>(count);
//            var subscriber = new SumValuesObserver(true);
//            map.Subscribe(subscriber);
//            var expectedSum = 0;
//            var sw = new Stopwatch();
//            sw.Start();
//            for (int i = 0; i < count; i++) {
//                map.Add(i, i);
//                expectedSum += i;
//            }
            
//            Assert.IsFalse(subscriber.IsCompleted);
//            map.Complete();
//            Assert.IsTrue(subscriber.Completed.Result);
//            sw.Stop();
//            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}");
//            Console.WriteLine($"MOps: {(count * 0.001) / sw.ElapsedMilliseconds}");

//            Assert.AreEqual(expectedSum, subscriber.Sum);
//            Assert.IsTrue(subscriber.IsCompleted);
//        }

//        //[Test]
//        //public void CouldPassErrorToObserver() {
//        //    CouldPassErrorToObserver(new SortedMap<int, int>());
//        //    //CouldPassErrorToObserver(new SortedChunkedMap<int, int>());
//        //}

//        //public void CouldPassErrorToObserver(IMutableSeries<int, int> map) {
//        //    var subscriber = new SumValuesObserver();
//        //    var cursor = map.ReadOnly().GetCursor();
//        //    cursor.Source.Subscribe(subscriber);
//        //    var expectedSum = 0;
//        //    for (int i = 0; i < 10; i++) {
//        //        map.Add(i, i);
//        //        expectedSum += i;
//        //    }
//        //    Assert.AreEqual(expectedSum, subscriber.Sum);
//        //    Assert.IsTrue(subscriber.Exception == null);
//        //    try {
//        //        map.AddLast(-1, -1);
//        //    } catch {
//        //    }
//        //    Assert.IsTrue(subscriber.Exception is OutOfOrderKeyException<int>);
//        //    Assert.AreEqual(-1, (subscriber.Exception as OutOfOrderKeyException<int>).NewKey);
//        //    Assert.AreEqual(9, (subscriber.Exception as OutOfOrderKeyException<int>).CurrentKey);
//        //}
//    }
//}
