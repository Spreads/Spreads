using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using Spreads;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Dapper;
using Spreads.Storage;

namespace Spreads.Extensions.Tests {

    [TestFixture]
    public class StorageTests {
        private IDbConnection _connection;

        [SetUp]
        public void CreateConnection()
        {
            var server = "localhost";
            var database = "spreads";
            var uid = "spreads";
            var password = "spreads";
            string connectionString;
            connectionString = "SERVER=" + server + ";" + "DATABASE=" +
            database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

            _connection = new MySqlConnection(connectionString);
            _connection.Open();
        }


        [Test]
        public void CouldConnectToStorage()
        {
            var two = _connection.Query<int>("SELECT 1+1;").Single();
            Assert.AreEqual(2, two);
        }


        [Test]
        public void CouldCreateMySqlSeriesStorage()
        {
            var storage = new MySqlSeriesStorage(_connection);
            var intMap = storage.GetPersistentOrderedMap<int, int>("int_map");
           
        }


        [Test]
        public void CouldCRUDMySqlSeriesStorage() {
            var storage = new MySqlSeriesStorage(_connection, idTableName: "test_series_ids", chunkTableName: "test_series_chunks");
            var timeseries = storage.GetPersistentOrderedMap<DateTime, double>("test_timeseries");

            if (!timeseries.IsEmpty)
            {
                // Remove all values
                timeseries.RemoveMany(timeseries.First.Key, Lookup.GE);
            }

            var sw = new Stopwatch();
            var count = 1000000;
            Console.WriteLine($"Count: {count}");

            var date = DateTime.UtcNow.Date;
            var rng = new System.Random();

            sw.Start();
            for (int i = 0; i < count; i++)
            {
                timeseries.Add(date, Math.Round((double)i + rng.NextDouble(), 2));
                date = date.AddMilliseconds(rng.Next(1, 10));
            }
            timeseries.Flush();
            sw.Stop();
            Console.WriteLine($"Writes, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");


            sw.Restart();
            var sum = 0.0;
            var storage2 = new MySqlSeriesStorage(_connection, idTableName: "test_series_ids", chunkTableName: "test_series_chunks");
            var timeseries2 = storage2.GetPersistentOrderedMap<DateTime, double>("test_timeseries");
            foreach (var kvp in timeseries2)
            {
                sum += kvp.Value;
            }
            sw.Stop();
            Console.WriteLine($"Reads, Mops: {count * 0.001 / sw.ElapsedMilliseconds}");


            var mySqlCount = _connection.ExecuteScalar<long>($"SELECT sum(count) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Count in MySQL: {mySqlCount}");
            Assert.AreEqual(count, mySqlCount);
            var mySqlSize = _connection.ExecuteScalar<long>($"SELECT sum(length(ChunkValue)) FROM {storage.ChunkTableName} where id = (SELECT id from {storage.IdTableName} where TextId = 'test_timeseries'); ");
            Console.WriteLine($"Memory size: {count * 16}; MySQL net blob size: {mySqlSize}; comp ratio: {Math.Round(count * 16.0/ mySqlSize *1.0, 2)}");
        }
    }
}
