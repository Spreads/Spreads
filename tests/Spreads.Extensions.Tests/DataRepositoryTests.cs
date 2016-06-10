using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Storage;

namespace Spreads.Extensions.Tests {
    [TestFixture]
    public class DataRepositoryTests {

        [Test]
        public void CouldCreateRepositoryAndGetSeries() {
            using (var repo = new DataRepository("../SeriesRepositoryTests")) {
                var ps = repo.WriteSeries<DateTime, double>("test_CouldGetPersistentSeries").Result;
                Assert.AreEqual(ps.Count, ps.Version);
                var initialVersion = ps.Version;
                ps.Add(DateTime.Now, 123.45);
                Console.WriteLine($"Count: {ps.Count}, version: {ps.Version}");
                Assert.AreEqual(initialVersion + 1, ps.Version);
                Assert.AreEqual(ps.Count, ps.Version);
            }
        }


        [Test]
        public void CouldCreateRepositoryAndGetMap() {
            using (var repo = new DataRepository("../SeriesRepositoryTests"))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests")) {
                var map = repo.WriteMap<long, long>("test_map", 1000).Result;
                var map2 = repo2.WriteMap<long, long>("test_map", 1000).Result;
                map[42] = 43;
                Assert.AreEqual(43, map[42]);
                Assert.AreEqual(43, map2[42]);
            }
        }



        [Test]
        public void CouldCreateRepositoryAndGetSeriesManyTimes() {
            for (int i = 0; i < 100; i++) {
                CouldCreateRepositoryAndGetSeries();
                GC.Collect(3, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }
        }


        [Test, Ignore]
        public void CouldCreateTwoRepositoriesAndGetSeries() {
            CouldCreateTwoRepositoriesAndGetSeries(123);
        }

        public void CouldCreateTwoRepositoriesAndGetSeries(int i) {

            using (var repo = new DataRepository("../SeriesRepositoryTests"))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests")) {
                // this read and write series have the same underlying instance inside the repo
                // the reead series are just wrapped with .ReadOnly()
                var psRead = repo2.ReadSeries<DateTime, double>("test_CouldGetPersistentSeries").Result;
                var readCursor = psRead.GetCursor();
                readCursor.MoveLast();
                var ps = repo.WriteSeries<DateTime, double>("test_CouldGetPersistentSeries").Result;
                Assert.AreEqual(ps.Count, ps.Version);
                var initialVersion = ps.Version;
                ps.Add(DateTime.UtcNow, i);
                Console.WriteLine($"Count: {ps.Count}, version: {ps.Version}");
                Assert.AreEqual(initialVersion + 1, ps.Version);
                Assert.AreEqual(ps.Count, ps.Version);
                Assert.IsFalse(psRead.IsEmpty);

                var lastRead = readCursor.MoveNext(CancellationToken.None).Result;

                if (!ps.Last.Equals(readCursor.Current)) {
                    Console.WriteLine($"{ps.Last.Key.Ticks} - {readCursor.Current.Key.Ticks}");
                }
                Assert.AreEqual(ps.Last, readCursor.Current);
            }
        }

        [Test, Ignore]
        public void CouldCreateTwoRepositoriesAndGetSeriesManyTimes() {
            for (int i = 0; i < 10000; i++) {
                CouldCreateTwoRepositoriesAndGetSeries(i);
            }
        }

        [Test, Ignore]
        public void CouldCreateTwoRepositoriesAndSynchronizeSeries() {
            
            using (var repo = new DataRepository("../SeriesRepositoryTests", 100))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests", 100)) {
                for (int rounds = 0; rounds < 1000; rounds++) {

                    var sw = new Stopwatch();

                    // this read and write series have the same underlying instance inside the repo
                    // the reead series are just wrapped with .ReadOnly()
                    var psRead =
                        repo2.ReadSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var readCursor = psRead.GetCursor();
                    readCursor.MoveLast();
                    Trace.WriteLine(readCursor.Current);
                    var ps =
                        repo.WriteSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var start = ps.IsEmpty ? DateTime.UtcNow : ps.Last.Key;
                    var count = 1000000;

                    sw.Start();

                    var readerTask = Task.Run(async () => {
                        var cnt = 0;
                        while (cnt < count && await readCursor.MoveNext(CancellationToken.None)) {
                            if (readCursor.Current.Value != cnt) Assert.AreEqual(cnt, readCursor.Current.Value);
                            if (readCursor.Current.Key != start.AddTicks(cnt + 1)) Assert.AreEqual(readCursor.Current.Key, start.AddTicks(cnt + 1));
                            cnt++;
                        }
                    });

                    for (int i = 0; i < count; i++) {
                        ps.Add(start.AddTicks(i + 1), i);
                    }

                    while (!readerTask.Wait(2000)) {
                        Trace.WriteLine("Timeout");
                        Trace.WriteLine($"Cursor: {readCursor.CurrentKey} - {readCursor.CurrentValue}");
                    }


                    sw.Stop();
                    Trace.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
                    Trace.WriteLine($"Round: {rounds}");
                }
            }
        }



        class SumObserver : IObserver<int> {
            private readonly int _count;
            public TaskCompletionSource<int> Tcs { get; }

            public SumObserver(int count)
            {
                _count = count - 1;
                Tcs = new TaskCompletionSource<int>();
            }

            public int Sum { get; private set; }

            public void OnNext(int value) {
                Sum += value;
                //if(value % 1000 == 0) Console.WriteLine(value);
                if (value == _count)
                {
                    Tcs.SetResult(Sum);
                }
            }

            public void OnError(Exception error) {
                throw new NotImplementedException();
            }

            public void OnCompleted() {
                throw new NotImplementedException();
            }
        }


        [Test, Ignore]
        public void CouldCreateTwoRepositoriesAndBroadcastMessages() {

            using (var repo = new DataRepository("../SeriesRepositoryTests", 100))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests", 100)) {
                for (int rounds = 0; rounds < 1000; rounds++) {

                    var sw = new Stopwatch();


                    // this read and write series have the same underlying instance inside the repo
                    // the reead series are just wrapped with .ReadOnly()
                    var observable = repo2.Broadcast<int>("test_CouldCreateTwoRepositoriesAndBroadcastMessages").Result;

                    var observer = repo.Broadcast<int>("test_CouldCreateTwoRepositoriesAndBroadcastMessages").Result;
                    var count = 1000000;
                     
                    sw.Start();

                    var sum = new SumObserver(count);

                    var unsubscribe = observable.Subscribe(sum);

                    for (int i = 0; i < count; i++) {
                        observer.OnNext(i);
                    }

                    sum.Tcs.Task.Wait();

                    Console.WriteLine("Sum: " + sum.Tcs.Task.Result);

                    sw.Stop();
                    Trace.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
                    Trace.WriteLine($"Round: {rounds}");
                    unsubscribe.Dispose();
                }
            }
        }


        [Test, Ignore]
        public void CouldReadSeriesAndCalculateStats() {

            using (var repo = new DataRepository("../SeriesRepositoryTests", 100))
                for (int rounds = 0; rounds < 10; rounds++) {

                    var sw = new Stopwatch();

                    var psRead = repo.ReadSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result.SMA(5, true); //.SMA(5, true)

                    var readCursor = psRead.GetCursor();
                    var ps = repo.WriteSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var trueCount = ps.Count;
                    Console.WriteLine("True count: " + trueCount);

                    var count = 1000;

                    sw.Start();
                    var cnt = 0;
                    var readerTask = Task.Run(() => {

                        while (readCursor.MoveNext()) {
                            cnt++;
                        }
                    });

                    while (!readerTask.Wait(20000)) {
                        Trace.WriteLine("Timeout");
                        Trace.WriteLine($"Cursor: {readCursor.CurrentKey} - {readCursor.CurrentValue}");
                    }
                    Console.WriteLine("Total count:" + cnt);

                    //Assert.AreEqual(trueCount - 1, cnt);
                    sw.Stop();
                    Trace.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
                    Trace.WriteLine($"Round: {rounds}");
                }
        }


        [Test, Ignore]
        public void CouldCreateTwoRepositoriesAndSynchronizeSeriesVarLength() {

            using (var repo = new DataRepository("../SeriesRepositoryTests", 100))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests", 100)) {
                for (int rounds = 0; rounds < 10; rounds++) {

                    var sw = new Stopwatch();


                    // this read and write series have the same underlying instance inside the repo
                    // the reead series are just wrapped with .ReadOnly()
                    var psRead =
                        repo2.ReadSeries<DateTime, string>("test_CouldCreateTwoRepositoriesAndSynchronizeSeriesVarLength").Result;
                    var readCursor = psRead.GetCursor();
                    readCursor.MoveLast();
                    Trace.WriteLine(readCursor.Current);
                    var ps =
                        repo.WriteSeries<DateTime, string>("test_CouldCreateTwoRepositoriesAndSynchronizeSeriesVarLength").Result;
                    var start = ps.IsEmpty ? DateTime.UtcNow : ps.Last.Key;
                    var count = 1000000;

                    sw.Start();

                    var readerTask = Task.Run(async () => {
                        var cnt = 0;
                        while (cnt < count && await readCursor.MoveNext(CancellationToken.None)) {
                            //if (readCursor.Current.Value != readCursor.Current.Key.ToString()) Assert.AreEqual(readCursor.Current.Key.Year.ToString(), readCursor.Current.Value);
                            //if (readCursor.Current.Key != start.AddTicks(cnt + 1)) Assert.AreEqual(readCursor.Current.Key, start.AddTicks(cnt + 1));
                            cnt++;
                        }
                    });

                    for (int i = 0; i < count; i++) {
                        var k = start.AddTicks(i + 1);
                        ps.Add(k, k.Year.ToString());
                    }

                    while (!readerTask.Wait(2000)) {
                        Trace.WriteLine("Timeout");
                        Trace.WriteLine($"Cursor: {readCursor.CurrentKey} - {readCursor.CurrentValue}");
                    }


                    sw.Stop();
                    Trace.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
                    Trace.WriteLine($"Round: {rounds}");
                }
            }
        }

        //[Test]
        //public void CouldCreateTwoRepositoriesAndSynchronizeSeriesManyTimes() {
        //    for (int i = 0; i < 100; i++) {
        //        CouldCreateTwoRepositoriesAndSynchronizeSeries();
        //        GC.Collect(3, GCCollectionMode.Forced, true);
        //    }
        //}



        [Test, Ignore]
        public void CouldSynchronizeSeriesFromSingleRepo() {

            using (var repo = new DataRepository("../SeriesRepositoryTests", 100)) {
                for (int rounds = 0; rounds < 1; rounds++) {

                    var sw = new Stopwatch();
                    sw.Start();

                    // this read and write series have the same underlying instance inside the repo
                    // the reead series are just wrapped with .ReadOnly()
                    var psRead =
                        repo.ReadSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var readCursor = psRead.GetCursor();
                    readCursor.MoveLast();
                    Console.WriteLine(readCursor.Current);
                    // this should upgrade to writer
                    var ps =
                        repo.WriteSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var start = ps.IsEmpty ? DateTime.UtcNow : ps.Last.Key;

                    var count = 1000000;

                    var readerTask = Task.Run(async () => {
                        var cnt = 0;
                        while (cnt < count && await readCursor.MoveNext(CancellationToken.None)) {
                            if (readCursor.Current.Value != cnt) Assert.AreEqual(cnt, readCursor.Current.Value);
                            cnt++;
                        }
                    });

                    for (int i = 0; i < count; i++) {
                        ps.Add(start.AddTicks(i + 1), i);
                    }

                    readerTask.Wait();

                    sw.Stop();
                    Console.WriteLine($"Elapsed msec: {sw.ElapsedMilliseconds}");
                    Console.WriteLine($"Round: {rounds}");
                }
            }
        }


        [Test]
        public void ConductorSwitchWorks() {
            using (var repo = new DataRepository("../SeriesRepositoryTests"))
            using (var repo2 = new DataRepository("../SeriesRepositoryTests"))
            {
                Assert.IsTrue(repo.IsConductor);
                Assert.IsFalse(repo2.IsConductor);
            }

            // we have now disposed both repos

            using (var repo2 = new DataRepository("../SeriesRepositoryTests"))
            using (var repo = new DataRepository("../SeriesRepositoryTests"))
             {
                Assert.IsTrue(repo2.IsConductor);
                Assert.IsFalse(repo.IsConductor);
            }

            var repo3 = new DataRepository("../SeriesRepositoryTests");
            var repo4 = new DataRepository("../SeriesRepositoryTests");
            Assert.IsTrue(repo3.IsConductor);
            Assert.IsFalse(repo4.IsConductor);
            repo3.Dispose();
            //Thread.Sleep(1);
            Assert.IsTrue(repo4.IsConductor);

        }

        [Test]
        public void FrequestConductorSwitchWorks() {
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(i);
                var repo3 = new DataRepository("../SeriesRepositoryTests");
                var repo4 = new DataRepository("../SeriesRepositoryTests");
                Assert.IsTrue(repo3.IsConductor);
                Assert.IsFalse(repo4.IsConductor);
                repo3.Dispose();
                Thread.Sleep(15);
                Assert.IsTrue(repo4.IsConductor);
                repo4.Dispose();
            }
        }
    }
}
