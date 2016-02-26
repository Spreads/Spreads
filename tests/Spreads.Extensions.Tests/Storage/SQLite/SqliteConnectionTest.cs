// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.IO;
using System.Reflection;
using Dapper;
using NUnit.Framework;
using Microsoft.Data.Sqlite.Utilities;
using static Microsoft.Data.Sqlite.TestUtilities.Constants;

namespace Microsoft.Data.Sqlite
{
    [TestFixture]
    public class SqliteConnectionTest
    {
        [SetUp]
        public void Init()
        {
            var bs = Bootstrap.Bootstrapper.Instance;
        }

        [Test]
        public void Ctor_sets_connection_string()
        {
            var connectionString = "Data Source=test.db";

            var connection = new SqliteConnection(connectionString);

            Assert.AreEqual(connectionString, connection.ConnectionString);
        }

        [Test]
        public void ConnectionString_setter_throws_when_open()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => connection.ConnectionString = "Data Source=test.db");

                Assert.AreEqual(Strings.ConnectionStringRequiresClosedConnection, ex.Message);
            }
        }

        [Test]
        public void ConnectionString_gets_and_sets_value()
        {
            var connection = new SqliteConnection();
            var connectionString = "Data Source=test.db";

            connection.ConnectionString = connectionString;

            Assert.AreEqual(connectionString, connection.ConnectionString);
        }

        [Test]
        public void Database_returns_value()
        {
            var connection = new SqliteConnection();

            Assert.AreEqual("main", connection.Database);
        }

        [Test]
        public void DataSource_returns_connection_string_data_source_when_closed()
        {
            var connection = new SqliteConnection("Data Source=test.db");

            Assert.AreEqual("test.db", connection.DataSource);
        }
        

        [Test]
        public void ServerVersion_returns_value()
        {
            var connection = new SqliteConnection();

            var version = connection.ServerVersion;

            Assert.IsTrue(version.StartsWith("3."));
        }

        [Test]
        public void State_closed_by_default()
        {
            var connection = new SqliteConnection();

            Assert.AreEqual(ConnectionState.Closed, connection.State);
        }

        [Test]
        public void Open_throws_when_no_connection_string()
        {
            var connection = new SqliteConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => connection.Open());

            Assert.AreEqual(Strings.OpenRequiresSetConnectionString, ex.Message);
        }

        [Test]
        public void Open_adjusts_relative_path()
        {
            var connection = new SqliteConnection("Filename=./local.db");
            connection.Open();
            Assert.AreEqual(Path.Combine(AppDomain.CurrentDomain.BaseDirectory , "local.db"), connection.DataSource);
        }

        [Test]
        public void Open_throws_when_error()
        {
            var connection = new SqliteConnection("Data Source=file:data.db?mode=invalidmode");

            var ex = Assert.Throws<SqliteException>(() => connection.Open());

            Assert.AreEqual(SQLITE_ERROR, ex.SqliteErrorCode);
        }

        [Test]
        public void Open_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.AreEqual(connection, sender);
                        Assert.AreEqual(ConnectionState.Closed, e.OriginalState);
                        Assert.AreEqual(ConnectionState.Open, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Open();

                    Assert.True(raised);
                    Assert.AreEqual(ConnectionState.Open, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Test]
        public void Open_works_when_readonly()
        {
            using (var connection = new SqliteConnection("Data Source=readonly.db"))
            {
                connection.Open();

                if (connection.ExecuteScalar<long>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Idomic';") == 0)
                {
                    (connection).ExecuteNonQuery("CREATE TABLE Idomic (Word TEXT);");
                }
            }

            using (var connection = new SqliteConnection("Data Source=readonly.db;Mode=ReadOnly"))
            {
                connection.Open();

                var ex = Assert.Throws<SqliteException>(
                    () => connection.ExecuteNonQuery("INSERT INTO Idomic VALUES ('arimfexendrapuse');"));

                Assert.AreEqual(SQLITE_READONLY, ex.SqliteErrorCode);
            }
        }

        [Test]
        public void Open_works_when_readwrite()
        {
            using (var connection = new SqliteConnection("Data Source=readwrite.db;Mode=ReadWrite"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.AreEqual(SQLITE_CANTOPEN, ex.SqliteErrorCode);
            }
        }

        [Test]
        public void Open_works_when_memory_shared()
        {
            var connectionString = "Data Source=people;Mode=Memory;Cache=Shared";

            using (var connection1 = new SqliteConnection(connectionString))
            {
                connection1.Open();

                connection1.ExecuteNonQuery(
                    "CREATE TABLE Person (Name TEXT);" +
                    "INSERT INTO Person VALUES ('Waldo');");

                using (var connection2 = new SqliteConnection(connectionString))
                {
                    connection2.Open();

                    var name = connection2.ExecuteScalar<string>("SELECT Name FROM Person;");
                    Assert.AreEqual("Waldo", name);
                }
            }
        }

        [Test]
        public void Open_works_when_uri()
        {
            using (var connection = new SqliteConnection("Data Source=file:readwrite.db?mode=rw"))
            {
                var ex = Assert.Throws<SqliteException>(() => connection.Open());

                Assert.AreEqual(SQLITE_CANTOPEN, ex.SqliteErrorCode);
            }
        }

        [Test]
        public void Close_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var raised = false;
                StateChangeEventHandler handler = (sender, e) =>
                    {
                        raised = true;

                        Assert.AreEqual(connection, sender);
                        Assert.AreEqual(ConnectionState.Open, e.OriginalState);
                        Assert.AreEqual(ConnectionState.Closed, e.CurrentState);
                    };

                connection.StateChange += handler;
                try
                {
                    connection.Close();

                    Assert.True(raised);
                    Assert.AreEqual(ConnectionState.Closed, connection.State);
                }
                finally
                {
                    connection.StateChange -= handler;
                }
            }
        }

        [Test]
        public void Close_can_be_called_before_open()
        {
            var connection = new SqliteConnection();

            connection.Close();
        }

        [Test]
        public void Close_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                connection.Close();
                connection.Close();
            }
        }

        [Test]
        public void Dispose_closes_connection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            connection.Dispose();

            Assert.AreEqual(ConnectionState.Closed, connection.State);
        }

        [Test]
        public void Dispose_can_be_called_more_than_once()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            connection.Dispose();
            connection.Dispose();
        }

        [Test]
        public void CreateCommand_returns_command()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();

                    Assert.NotNull(command);
                    Assert.AreSame(connection, command.Connection);
                    Assert.AreSame(transaction, command.Transaction);
                }
            }
        }

        [Test]
        public void BeginTransaction_throws_when_closed()
        {
            var connection = new SqliteConnection();

            var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

            Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("BeginTransaction"), ex.Message);
        }

        [Test]
        public void BeginTransaction_throws_when_parallel_transaction()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (connection.BeginTransaction())
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => connection.BeginTransaction());

                    Assert.AreEqual(Strings.ParallelTransactionsNotSupported, ex.Message);
                }
            }
        }

        [Test]
        public void BeginTransaction_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    Assert.NotNull(transaction);
                    Assert.AreEqual(connection, transaction.Connection);
                    Assert.AreEqual(IsolationLevel.Serializable, transaction.IsolationLevel);
                }
            }
        }

        [Test]
        public void ChangeDatabase_not_supported()
        {
            using (var connection = new SqliteConnection())
            {
                Assert.Throws<NotSupportedException>(() => connection.ChangeDatabase("new"));
            }
        }

        [Test]
        public void Mars_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var command1 = connection.CreateCommand();
                command1.CommandText = "SELECT '1A' UNION SELECT '1B';";

                using (var reader1 = command1.ExecuteReader())
                {
                    reader1.Read();
                    Assert.AreEqual("1A", reader1.GetString(0));

                    var command2 = connection.CreateCommand();
                    command2.CommandText = "SELECT '2A' UNION SELECT '2B';";

                    using (var reader2 = command2.ExecuteReader())
                    {
                        reader2.Read();
                        Assert.AreEqual("2A", reader2.GetString(0));

                        reader1.Read();
                        Assert.AreEqual("1B", reader1.GetString(0));

                        reader2.Read();
                        Assert.AreEqual("2B", reader2.GetString(0));
                    }
                }
            }
        }

       

        [Test]
        public void EnableExtensions_throws_when_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.EnableExtensions());
                Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("EnableExtensions"), ex.Message);
            }
        }
    }
}
