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
            var storage = SeriesStorage.Default;
            storage.GetPersistentOrderedMap<int, int>("int_map");

            Assert.Throws(typeof (ArgumentException), () =>
            {
                try
                {
                    var map = storage.GetPersistentOrderedMap<int, double>("int_map");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw;
                }
            });
        }


        [Test, Ignore]
        public void CouldCRUDSeriesStorage() {
            var storage = SeriesStorage.Default;
            var timeseries = storage.GetPersistentOrderedMap<DateTime, double>("test_timeseries");

            if (!timeseries.IsEmpty) {
                // Remove all values
                timeseries.RemoveMany(timeseries.First.Key, Lookup.GE);
            }

            var sw = new Stopwatch();
            var count = 1000000L;
            Console.WriteLine($"Count: {count}");

            var date = DateTime.UtcNow.Date;
            var rng = new Random();

            sw.Start();
            for (int i = 0; i < count; i++) {
                timeseries.Add(date, Math.Round(i + rng.NextDouble(), 2));
                date = date.AddMilliseconds(rng.Next(1, 10));
                if (i%10000000 == 0)
                {
                    Console.WriteLine($"Wrote: {i}");
                }
            }
            timeseries.Flush();
            sw.Stop();
            Console.WriteLine($"Writes, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");


            sw.Restart();
            var sum = 0.0;
            var storage2 = new SeriesStorage($"Filename={Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "default.db")}");
            var timeseries2 = storage2.GetPersistentOrderedMap<DateTime, double>("test_timeseries");
            foreach (var kvp in timeseries2) {
                sum += kvp.Value;
            }
            Assert.IsTrue(sum > 0);
            sw.Stop();
            Console.WriteLine($"Reads, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");

            var _connection =
                new SqliteConnection(
                    $"Filename={Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "default.db")}");

            var sqlCount = _connection.ExecuteScalar<long>($"SELECT sum(count) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Count in SQLite: {sqlCount}");
            Assert.AreEqual(count, sqlCount);
            var sqlSize = _connection.ExecuteScalar<long>($"SELECT sum(length(ChunkValue)) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Memory size: {count * 16L}; SQLite net blob size: {sqlSize}; comp ratio: {Math.Round(count * 16.0 / sqlSize * 1.0, 2)}");
        }
    }
}
