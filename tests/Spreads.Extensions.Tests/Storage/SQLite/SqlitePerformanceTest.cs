using System;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite.Utilities;
using NUnit.Framework;
//using Dapper;


namespace Spreads.Extensions.Tests.Storage.SQLite {
    [TestFixture]
    public class SqlitePerformanceTest {
        [SetUp]
        public void Init() {
            var bs = Bootstrap.Bootstrapper.Instance;
        }


        [Test]
        public void InsertSpeed()
        {
            var connectionString =  "Data Source=perf_test.db;"; // "Data Source=:memory:";//

            using (var connection = new SqliteConnection(connectionString)) {
                connection.Open();
                //connection.ExecuteNonQuery("PRAGMA main.locking_mode=EXCLUSIVE;");
                connection.ExecuteNonQuery("PRAGMA main.page_size = 4096; ");
                connection.ExecuteNonQuery("PRAGMA main.cache_size = 10000;");
                connection.ExecuteNonQuery("PRAGMA synchronous = OFF;"); // NORMAL or OFF 20% faster
                connection.ExecuteNonQuery("PRAGMA journal_mode = WAL;");
                connection.ExecuteNonQuery("PRAGMA main.cache_size = 5000;");
                connection.ExecuteNonQuery("DROP TABLE IF EXISTS Numbers");
                connection.ExecuteNonQuery("CREATE TABLE Numbers (Key INTEGER, Value REAL, PRIMARY KEY(Key));");

                var sw = new Stopwatch();
                sw.Start();
                //var txn = connection.BeginTransaction();
                for (int i = 0; i < 100000; i++) {
                    connection.ExecuteNonQuery($"INSERT INTO Numbers VALUES ({i}, {i});");
                    //connection.Execute("INSERT INTO Numbers VALUES (@Key, @Value);",
                    //    new[] { new { Key = (long)i, Value = (double)i } });
                }
                //txn.Commit();
                sw.Stop();
                Console.WriteLine($"Elapsed, msec {sw.ElapsedMilliseconds}");

            }
        }


    }
}
