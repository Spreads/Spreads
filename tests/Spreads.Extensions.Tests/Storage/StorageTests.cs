using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using Spreads.Storage;

namespace Spreads.Extensions.Tests.Storage {

    [TestFixture, Ignore]
    public class StorageTests {


        [Test, Ignore]
        public void CouldCreateSeriesStorage() {
            var folder = Bootstrap.Bootstrapper.Instance.DataFolder;
            Console.WriteLine(folder);
            var storage = SeriesStorage.GetDefault("storage_test.db");
            storage.GetPersistentOrderedMap<int, int>("int_map");

            Assert.Throws(typeof(ArgumentException), () => {
                try {
                    var map = storage.GetPersistentOrderedMap<int, double>("int_map");
                } catch (Exception e) {
                    Console.WriteLine(e.Message);
                    throw;
                }
            });
        }


        [Test, Ignore]
        public void CouldCRUDSeriesStorage() {
            var storage = new SeriesStorage("Filename=../benchmark.db"); // SeriesStorage.Default;
            var timeseries = storage.GetPersistentOrderedMap<DateTime, double>("test_timeseries");

            Console.WriteLine(storage.Connection.DataSource);
            var start = DateTime.UtcNow;
            Console.WriteLine($"Started at: {start}");

            if (!timeseries.IsEmpty) {
                // Remove all values
                timeseries.RemoveMany(timeseries.First.Key, Lookup.GE);
            }

            var sw = new Stopwatch();
            var count = 50000000000L;
            Console.WriteLine($"Count: {count}");

            var date = DateTime.UtcNow.Date;
            var rng = new Random();
            sw.Start();
            for (long i = 0; i < count; i++) {
                timeseries.Add(date, Math.Round(i + rng.NextDouble(), 2));
                date = date.AddTicks(rng.Next(1, 100));
                if (i % 10000000 == 0)
                {
                    var msec = (DateTime.UtcNow - start).TotalMilliseconds;
                    var mops = i*0.001/ msec;
                    Console.WriteLine($"Wrote: {i} - {Math.Round((i * 1.0) / (count * 1.0), 4) * 100.0}% in {msec/1000} sec, Mops: {mops}");
                }
            }
            timeseries.Flush();
            Console.WriteLine($"Wrote: {count} - 100%");
            Console.WriteLine($"Finished at: {DateTime.UtcNow}");
            sw.Stop();
            Console.WriteLine($"Writes, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");


            sw.Restart();
            var sum = 0.0;
            var storage2 = new SeriesStorage("Filename=../benchmark.db"); // $"Filename={Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "default.db")}");
            var timeseries2 = storage2.GetPersistentOrderedMap<DateTime, double>("test_timeseries");
            foreach (var kvp in timeseries2) {
                sum += kvp.Value;
            }
            Assert.IsTrue(sum > 0);
            sw.Stop();
            Console.WriteLine($"Reads, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");

            var _connection =
                new SqliteConnection("Filename=../benchmark.db");
            //$"Filename={Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "default.db")}");

            var sqlCount = _connection.ExecuteScalar<long>($"SELECT sum(count) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Count in SQLite: {sqlCount}");
            Assert.AreEqual(count, sqlCount);
            var sqlSize = _connection.ExecuteScalar<long>($"SELECT sum(length(ChunkValue)) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Memory size: {count * 16L}; SQLite net blob size: {sqlSize}; comp ratio: {Math.Round(count * 16.0 / sqlSize * 1.0, 2)}");
        }
    }
}
