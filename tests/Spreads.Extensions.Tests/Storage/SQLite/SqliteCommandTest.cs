// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using Microsoft.Data.Sqlite.Utilities;
using NUnit.Framework;

using static Microsoft.Data.Sqlite.TestUtilities.Constants;

namespace Microsoft.Data.Sqlite
{
    [TestFixture]
    public class SqliteCommandTest
    {
        
        [Test]
        public void Ctor_sets_values()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    var command = new SqliteCommand("SELECT 1;", connection, transaction);

                    Assert.AreEqual("SELECT 1;", command.CommandText);
                    Assert.AreSame(connection, command.Connection);
                    Assert.AreSame(transaction, command.Transaction);
                }
            }
        }

        [Test]
        public void CommandType_text_by_default()
        {
            Assert.AreEqual(CommandType.Text, new SqliteCommand().CommandType);
        }

        [Test]
        public void CommandType_validates_value()
        {
            var ex = Assert.Throws<ArgumentException>(() => new SqliteCommand().CommandType = CommandType.StoredProcedure);

            Assert.AreEqual(Strings.FormatInvalidCommandType(CommandType.StoredProcedure), ex.Message);
        }

        [Test]
        public void Parameters_works()
        {
            var command = new SqliteCommand();

            var result = command.Parameters;

            Assert.NotNull(result);
            Assert.AreSame(result, command.Parameters);
        }

        [Test]
        public void CreateParameter_works()
        {
            Assert.NotNull(new SqliteCommand().CreateParameter());
        }

        [Test]
        public void Prepare_is_noop()
        {
            new SqliteCommand().Prepare();
        }

        [Test]
        public void ExecuteReader_throws_when_no_connection()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new SqliteCommand().ExecuteReader());

            Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteReader"), ex.Message);
        }

        [Test]
        public void ExecuteReader_throws_when_connection_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteReader());

                Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteReader"), ex.Message);
            }
        }

        [Test]
        public void ExecuteReader_throws_when_no_command_text()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteReader());

                Assert.AreEqual(Strings.FormatCallRequiresSetCommandText("ExecuteReader"), ex.Message);
            }
        }

        [Test]
        public void ExecuteReader_throws_on_error()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "INVALID";
                connection.Open();

                var ex = Assert.Throws<SqliteException>(() => command.ExecuteReader());

                Assert.AreEqual(SQLITE_ERROR, ex.SqliteErrorCode);
            }
        }

        [Test]
        public void ExecuteScalar_throws_when_no_connection()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new SqliteCommand().ExecuteScalar());

            Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteScalar"), ex.Message);
        }

        [Test]
        public void ExecuteScalar_throws_when_connection_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteScalar());

                Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteScalar"), ex.Message);
            }
        }

        [Test]
        public void ExecuteReader_throws_when_transaction_required()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                connection.Open();

                using (connection.BeginTransaction())
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => command.ExecuteReader());

                    Assert.AreEqual(Strings.TransactionRequired, ex.Message);
                }
            }
        }

        [Test]
        public void ExecuteScalar_throws_when_no_command_text()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteScalar());

                Assert.AreEqual(Strings.FormatCallRequiresSetCommandText("ExecuteScalar"), ex.Message);
            }
        }

        [Test]
        public void ExecuteScalar_returns_null_when_empty()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 WHERE 0 = 1;";
                connection.Open();

                Assert.Null(command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_long_when_integer()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                connection.Open();

                Assert.AreEqual(1L, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_double_when_real()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 3.14;";
                connection.Open();

                Assert.AreEqual(3.14, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_string_when_text()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 'test';";
                connection.Open();

                Assert.AreEqual("test", command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_byte_array_when_blob()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT x'7e57';";
                connection.Open();

                Assert.AreEqual(new byte[] { 0x7e, 0x57 }, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_DBNull_when_null()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT NULL;";
                connection.Open();

                Assert.AreEqual(DBNull.Value, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteReader_binds_parameters()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT @Parameter;";
                command.Parameters.AddWithValue("@Parameter", 1);
                connection.Open();

                Assert.AreEqual(1L, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteReader_throws_when_parameter_unset()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT @Parameter;";
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar());
                Assert.AreEqual(Strings.FormatMissingParameters("@Parameter"), ex.Message);
            }
        }

        [Test]
        public void ExecuteScalar_returns_long_when_batching()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 42; SELECT 43;";
                connection.Open();

                Assert.AreEqual(42L, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_long_when_multiple_columns()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 42, 43;";
                connection.Open();

                Assert.AreEqual(42L, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteScalar_returns_long_when_multiple_rows()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 42 UNION SELECT 43;";
                connection.Open();

                Assert.AreEqual(42L, command.ExecuteScalar());
            }
        }

        [Test]
        public void ExecuteNonQuery_throws_when_no_connection()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new SqliteCommand().ExecuteNonQuery());

            Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteNonQuery"), ex.Message);
        }

        [Test]
        public void ExecuteNonQuery_throws_when_connection_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteNonQuery());

                Assert.AreEqual(Strings.FormatCallRequiresOpenConnection("ExecuteNonQuery"), ex.Message);
            }
        }

        [Test]
        public void ExecuteNonQuery_throws_when_no_command_text()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var ex = Assert.Throws<InvalidOperationException>(() => connection.CreateCommand().ExecuteNonQuery());

                Assert.AreEqual(Strings.FormatCallRequiresSetCommandText("ExecuteNonQuery"), ex.Message);
            }
        }

        [Test]
        public void ExecuteReader_throws_when_transaction_mismatched()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                connection.Open();

                using (var otherConnection = new SqliteConnection("Data Source=:memory:"))
                {
                    otherConnection.Open();

                    using (var transction = otherConnection.BeginTransaction())
                    {
                        command.Transaction = transction;

                        var ex = Assert.Throws<InvalidOperationException>(() => command.ExecuteReader());

                        Assert.AreEqual(Strings.TransactionConnectionMismatch, ex.Message);
                    }
                }
            }
        }

        [Test]
        public void ExecuteNonQuery_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                connection.Open();

                Assert.AreEqual(-1, command.ExecuteNonQuery());
            }
        }

        [Test]
        public void ExecuteReader_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1;";
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    Assert.NotNull(reader);
                }
            }
        }

        [Test]
        public void ExecuteReader_skips_DML_statements()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.ExecuteNonQuery("CREATE TABLE Test(Value);");

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Test VALUES(1);
                    SELECT 1;";

                using (var reader = command.ExecuteReader())
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.AreEqual(1L, reader.GetInt64(0));
                }
            }
        }

        [Test]
        public void ExecuteReader_works_when_comments()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "-- TODO: Write SQL";
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    Assert.False(reader.HasRows);
                    Assert.AreEqual(-1, reader.RecordsAffected);
                }
            }
        }

        [Test]
        public void ExecuteReader_works_when_trailing_comments()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 0; -- My favorite number";
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    var hasResult = reader.NextResult();
                    Assert.False(hasResult);
                }
            }
        }

        [Test]
        public void Cancel_not_supported()
        {
            Assert.Throws<NotSupportedException>(() => new SqliteCommand().Cancel());
        }

        [Test]
        public void ExecuteReader_supports_SequentialAccess()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 0;";
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    var hasResult = reader.NextResult();
                    Assert.False(hasResult);
                }
            }
        }
        
        [Test]
        public void ExecuteReader_supports_SingleResult()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 0;";
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SingleResult))
                {
                    var hasResult = reader.NextResult();
                    Assert.False(hasResult);
                }
            }
        }

        [Test]
        public void ExecuteReader_supports_SingleRow()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 0;";
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                {
                    var hasResult = reader.NextResult();
                    Assert.False(hasResult);
                }
            }
        }

        [Test]
        public void ExecuteReader_supports_CloseConnection()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 0;";
                connection.Open();

                using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    var hasResult = reader.NextResult();
                    Assert.False(hasResult);
                }
                Assert.AreEqual(ConnectionState.Closed, connection.State);
            }
        }

       
    }
}
