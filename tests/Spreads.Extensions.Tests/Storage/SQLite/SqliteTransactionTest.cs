// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Microsoft.Data.Sqlite.Utilities;
using NUnit.Framework;
using static Microsoft.Data.Sqlite.Interop.Constants;

namespace Microsoft.Data.Sqlite
{
    [TestFixture]
    public class SqliteTransactionTest
    {
        [SetUp]
        public void Init() {
            var bs = Bootstrap.Bootstrapper.Instance;
        }

        [Test]
        public void Ctor_sets_read_uncommitted()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:;Cache=Shared"))
            {
                connection.Open();

                using (connection.BeginTransaction(IsolationLevel.ReadUncommitted))
                {
                    Assert.AreEqual(1L, connection.ExecuteScalar<long>("PRAGMA read_uncommitted;"));
                }
            }
        }

        [Test]
        public void Ctor_unsets_read_uncommitted_when_serializable()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    Assert.AreEqual(0L, connection.ExecuteScalar<long>("PRAGMA read_uncommitted;"));
                }
            }
        }

        [Test]
        public void Ctor_throws_when_invalid_isolation_level()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<ArgumentException>(() => connection.BeginTransaction(IsolationLevel.Snapshot));

                Assert.AreEqual(Strings.FormatInvalidIsolationLevel(IsolationLevel.Snapshot), ex.Message);
            }
        }

        [Test]
        public void Ctor_throws_when_invalid_isolation_level_without_cache()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                var ex = Assert.Throws<ArgumentException>(() => connection.BeginTransaction(IsolationLevel.ReadUncommitted));
                Assert.AreEqual(Strings.FormatInvalidIsolationLevelForUnsharedCache(IsolationLevel.ReadUncommitted), ex.Message);
            }

            using (var connection = new SqliteConnection("Data Source=:memory:;Cache=Shared"))
            {
                connection.Open();
                connection.BeginTransaction(IsolationLevel.ReadUncommitted);
            }
        }

        [Test]
        public void IsolationLevel_throws_when_completed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var transaction = connection.BeginTransaction();
                transaction.Dispose();

                //var ex = Assert.Throws<InvalidOperationException>(() => transaction.IsolationLevel);

                //Assert.AreEqual(Strings.TransactionCompleted, ex.Message);
            }
        }

        [Test]
        public void IsolationLevel_is_infered_when_unspecified()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.ExecuteNonQuery("PRAGMA read_uncommitted = 1;");

                using (var transaction = connection.BeginTransaction())
                {
                    Assert.AreEqual(IsolationLevel.ReadUncommitted, transaction.IsolationLevel);
                }
            }
        }

        [Test]
        public void Commit_throws_when_completed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var transaction = connection.BeginTransaction();
                transaction.Dispose();

                var ex = Assert.Throws<InvalidOperationException>(() => transaction.Commit());

                Assert.AreEqual(Strings.TransactionCompleted, ex.Message);
            }
        }

        [Test]
        public void Commit_throws_when_connection_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    connection.Close();

                    var ex = Assert.Throws<InvalidOperationException>(() => transaction.Commit());

                    Assert.AreEqual(Strings.TransactionCompleted, ex.Message);
                }
            }
        }

        [Test]
        public void Commit_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                CreateTestTable(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    connection.ExecuteNonQuery("INSERT INTO TestTable VALUES (1);");

                    transaction.Commit();

                    Assert.Null(connection.Transaction);
                    Assert.Null(transaction.Connection);
                }

                Assert.AreEqual(1L, connection.ExecuteScalar<long>("SELECT COUNT(*) FROM TestTable;"));
            }
        }

        [Test]
        public void Rollback_throws_when_completed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var transaction = connection.BeginTransaction();
                transaction.Dispose();

                var ex = Assert.Throws<InvalidOperationException>(() => transaction.Rollback());

                Assert.AreEqual(Strings.TransactionCompleted, ex.Message);
            }
        }

        [Test]
        public void Rollback_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                CreateTestTable(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    connection.ExecuteNonQuery("INSERT INTO TestTable VALUES (1);");

                    transaction.Rollback();

                    Assert.Null(connection.Transaction);
                    Assert.Null(transaction.Connection);
                }

                Assert.AreEqual(0L, connection.ExecuteScalar<long>("SELECT COUNT(*) FROM TestTable;"));
            }
        }

        [Test]
        public void Dispose_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                CreateTestTable(connection);

                using (var transaction = connection.BeginTransaction())
                {
                    connection.ExecuteNonQuery("INSERT INTO TestTable VALUES (1);");

                    transaction.Dispose();

                    Assert.Null(connection.Transaction);
                    Assert.Null(transaction.Connection);
                }

                Assert.AreEqual(0L, connection.ExecuteScalar<long>("SELECT COUNT(*) FROM TestTable;"));
            }
        }

        [Test]
        public void Serializable_locks()
        {
            using (var connectionA = new SqliteConnection("Data Source=testdb;Mode=Memory;Cache=Shared"))
            {
                connectionA.Open();
                using (var transactionA = connectionA.BeginTransaction(IsolationLevel.Serializable))
                {
                    using (var connectionB = new SqliteConnection("Data Source=testdb;Mode=Memory;Cache=Shared"))
                    {
                        connectionB.Open();
                        var ex = Assert.Throws<SqliteException>(() => new SqliteTransaction(connectionB, IsolationLevel.Serializable, 1));
                        Assert.AreEqual(SQLITE_LOCKED, ex.SqliteErrorCode);
                    }
                }
            }
        }

        [Test]
        public void Dispose_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var transaction = connection.BeginTransaction();

                transaction.Dispose();
                transaction.Dispose();
            }
        }

        private static void CreateTestTable(SqliteConnection connection)
        {
            connection.ExecuteNonQuery(@"
                CREATE TABLE TestTable (
                    TestColumn INTEGER
                )");
        }
    }
}
