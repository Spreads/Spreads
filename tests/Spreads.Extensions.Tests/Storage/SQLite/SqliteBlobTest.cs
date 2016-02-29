using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite.Interop;
using Microsoft.Data.Sqlite.Utilities;
using NUnit.Framework;
//using Dapper;


namespace Spreads.Extensions.Tests.Storage.SQLite {
    [TestFixture]
    public class SqliteBlobTest {

        [Test]
        public void CouldReadWriteBlob() {
            var connectionString = "Data Source=perf_test.db;"; // "Data Source=:memory:";//

            using (var connection = new SqliteConnection(connectionString)) {
                connection.Open();
                //connection.ExecuteNonQuery("PRAGMA main.locking_mode=EXCLUSIVE;");
                connection.ExecuteNonQuery("PRAGMA main.page_size = 4096; ");
                connection.ExecuteNonQuery("PRAGMA main.cache_size = 10000;");
                connection.ExecuteNonQuery("PRAGMA synchronous = OFF;"); // NORMAL or OFF 20% faster
                connection.ExecuteNonQuery("PRAGMA journal_mode = WAL;");
                connection.ExecuteNonQuery("PRAGMA main.cache_size = 5000;");
                connection.ExecuteNonQuery("DROP TABLE IF EXISTS Blobs");
                connection.ExecuteNonQuery("CREATE TABLE Blobs (Key INTEGER, Value BLOB, PRIMARY KEY(Key));");

                connection.ExecuteNonQuery($"INSERT INTO Blobs VALUES (1, zeroblob(1024*1024));");



                Sqlite3BlobHandle blob;
                var rc = NativeMethods.sqlite3_blob_open(connection.DbHandle, "main", "Blobs", "Value", 1,
                    Constants.SQLITE_OPEN_READWRITE, out blob);


                var length = NativeMethods.sqlite3_blob_bytes(blob);
                Assert.AreEqual(1024 * 1024, length);

                unsafe
                {
                    long[] source = new long[1];
                    source[0] = 42;
                    fixed (long* ptrSrc = &source[0])
                    {
                        var written = NativeMethods.sqlite3_blob_read(blob, (IntPtr)ptrSrc, 8, 0);
                        Assert.AreEqual(Constants.SQLITE_OK, written);
                    }

                }


                var task = Task.Run(() => {
                    Thread.Sleep(500);
                    using (var connection2 = new SqliteConnection(connectionString)) {
                        // reader
                        connection2.Open();
                        Sqlite3BlobHandle blob2;

                        var rc2 = NativeMethods.sqlite3_blob_open(connection2.DbHandle, "main", "Blobs", "Value", 1,
                            Constants.SQLITE_OPEN_READONLY, out blob2);
                        Assert.AreEqual(Constants.SQLITE_BUSY, rc2);
                        unsafe
                        {
                            long[] dest = new long[1];
                            fixed (long* ptrDest = &dest[0])
                            {
                                var read = NativeMethods.sqlite3_blob_read(blob2, (IntPtr)ptrDest, 8, 0);
                                Assert.AreEqual(Constants.SQLITE_MISUSE, read);
                            }

                        }

                        blob2.Dispose();

                    }
                });


                task.Wait();

                blob.Dispose();

            }
        }


    }
}
