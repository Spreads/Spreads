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
    public class SeriesRepositoryTests {

        [Test]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void CouldCreateRepositoryAndGetSeries() {
            using (var repo = new SeriesRepository("../SeriesRepositoryTests")) {
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
        public void CouldCreateRepositoryAndGetSeriesManyTimes() {
            for (int i = 0; i < 100; i++) {
                CouldCreateRepositoryAndGetSeries();
                GC.Collect(3, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }
        }


        [Test]
        public void CouldCreateTwoRepositoriesAndGetSeries() {
            CouldCreateTwoRepositoriesAndGetSeries(123);
        }

        public void CouldCreateTwoRepositoriesAndGetSeries(int i) {

            using (var repo = new SeriesRepository("../SeriesRepositoryTests"))
            using (var repo2 = new SeriesRepository("../SeriesRepositoryTests")) {
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

        [Test]
        public void CouldCreateTwoRepositoriesAndGetSeriesManyTimes() {
            for (int i = 0; i < 10000; i++) {
                CouldCreateTwoRepositoriesAndGetSeries(i);
            }
        }

        [Test]
        public void CouldCreateTwoRepositoriesAndSynchronizeSeries() {

            using (var repo = new SeriesRepository("../SeriesRepositoryTests", 100))
            using (var repo2 = new SeriesRepository("../SeriesRepositoryTests", 100)) {
                for (int rounds = 0; rounds < 10; rounds++) {

                    var sw = new Stopwatch();
                    

                    // this read and write series have the same underlying instance inside the repo
                    // the reead series are just wrapped with .ReadOnly()
                    var psRead =
                        repo2.ReadSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var readCursor = psRead.GetCursor();
                    readCursor.MoveLast();
                    Console.WriteLine(readCursor.Current);
                    var ps =
                        repo.WriteSeries<DateTime, double>("test_CouldCreateTwoRepositoriesAndSynchronizeSeries").Result;
                    var start = ps.IsEmpty ? DateTime.UtcNow : ps.Last.Key;
                    var count = 1000000;

                    sw.Start();

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

        //[Test]
        //public void CouldCreateTwoRepositoriesAndSynchronizeSeriesManyTimes() {
        //    for (int i = 0; i < 100; i++) {
        //        CouldCreateTwoRepositoriesAndSynchronizeSeries();
        //        GC.Collect(3, GCCollectionMode.Forced, true);
        //    }
        //}



        [Test]
        public void CouldSynchronizeSeriesFromSingleRepo() {

            using (var repo = new SeriesRepository("../SeriesRepositoryTests", 100)) {
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
    }
}
