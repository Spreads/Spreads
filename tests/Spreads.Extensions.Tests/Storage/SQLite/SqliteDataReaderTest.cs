// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using Microsoft.Data.Sqlite.TestUtilities;
using Microsoft.Data.Sqlite.Utilities;
using NUnit.Framework;

namespace Microsoft.Data.Sqlite
{
    [TestFixture]
    public class SqliteDataReaderTest
    {

        [Test]
        public void Depth_returns_zero()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    Assert.AreEqual(0, reader.Depth);
                }
            }
        }

        [Test]
        public void FieldCount_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    Assert.AreEqual(1, reader.FieldCount);
                }
            }
        }

        [Test]
        public void FieldCount_throws_when_closed() => X_throws_when_closed(r => { var x = r.FieldCount; }, "FieldCount");

        [Test]
        public void GetBoolean_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetBoolean(0),
                true);

        [Test]
        public void GetByte_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetByte(0),
                (byte)1);

        [Test]
        public void GetBytes_not_supported()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT x'7E57';"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    var buffer = new byte[2];
                    Assert.Throws<NotSupportedException>(() => reader.GetBytes(0, 0, buffer, 0, buffer.Length));
                }
            }
        }

        [Test]
        public void GetChar_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetChar(0),
                (char)1);

        [Test]
        public void GetChars_not_supported()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 'test';"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    var buffer = new char[4];
                    Assert.Throws<NotSupportedException>(() => reader.GetChars(0, 0, buffer, 0, buffer.Length));
                }
            }
        }

        [Test]
        public void GetDateTime_works() =>
            GetX_works(
                "SELECT '2014-04-15 10:47:16';",
                r => r.GetDateTime(0),
                new DateTime(2014, 4, 15, 10, 47, 16));

        [Theory]
        [TestCase("SELECT 1;", "INTEGER")]
        [TestCase("SELECT 3.14;", "REAL")]
        [TestCase("SELECT 'test';", "TEXT")]
        [TestCase("SELECT X'7E57';", "BLOB")]
        [TestCase("SELECT NULL;", "INTEGER")]
        public void GetDataTypeName_works(string sql, string expected)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader(sql))
                {
                    Assert.AreEqual(expected, reader.GetDataTypeName(0));
                }
            }
        }

        [Test]
        public void GetDataTypeName_works_when_column()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.ExecuteNonQuery("CREATE TABLE Person ( Name nvarchar(4000) );");

                using (var reader = connection.ExecuteReader("SELECT Name FROM Person;"))
                {
                    Assert.AreEqual("nvarchar", reader.GetDataTypeName(0));
                }
            }
        }

        [Test]
        public void GetDataTypeName_throws_when_ordinal_out_of_range()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var ex = Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetDataTypeName(1));

                    Assert.AreEqual("ordinal", ex.ParamName);
                    Assert.AreEqual(1, ex.ActualValue);
                }
            }
        }

        [Test]
        public void GetDataTypeName_throws_when_closed() => X_throws_when_closed(r => r.GetDataTypeName(0), "GetDataTypeName");

        [Test]
        public void GetDecimal_works() =>
            GetX_works(
                "SELECT '3.14';",
                r => r.GetDecimal(0),
                3.14m);

        [Test]
        public void GetDouble_works() =>
            GetX_works(
                "SELECT 3.14;",
                r => r.GetDouble(0),
                3.14);

        [Test]
        public void GetDouble_throws_when_null() =>
            GetX_throws_when_null(
                r => r.GetDouble(0));

#if DNXCORE50
        [Test]
        public void GetEnumerator_not_implemented()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.Throws<NotImplementedException>(() => reader.GetEnumerator());
                }
            }
        }
#else
        [Test]
        public void GetEnumerator_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.NotNull(reader.GetEnumerator());
                }
            }
        }
#endif

        [Theory]
        [TestCase("SELECT 1;", Result = true)]
        [TestCase("SELECT 1;", Result = (byte)1)]
        [TestCase("SELECT 1;", Result = (char)1)]
        [TestCase("SELECT 3.14;", Result = 3.14)]
        [TestCase("SELECT 3;", Result = 3f)]
        [TestCase("SELECT 1;", Result = 1)]
        [TestCase("SELECT 1;", Result = 1L)]
        [TestCase("SELECT 1;", Result = (sbyte)1)]
        [TestCase("SELECT 1;", Result = (short)1)]
        [TestCase("SELECT 'test';", Result = "test")]
        [TestCase("SELECT 1;", Result = 1u)]
        [TestCase("SELECT 1;", Result = 1ul)]
        [TestCase("SELECT 1;", Result = (ushort)1)]
        public void GetFieldValue_works<T>(string sql, T expected)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader(sql))
                {
                    var hasData = reader.Read();

                    Assert.True(hasData);
                    Assert.AreEqual(expected, reader.GetFieldValue<T>(0));
                }
            }
        }

        [Test]
        public void GetFieldValue_of_byteArray_works() =>
            GetFieldValue_works(
                "SELECT X'7E57';",
                new byte[] { 0x7e, 0x57 });

        [Test]
        public void GetFieldValue_of_byteArray_empty() =>
            GetFieldValue_works(
                "SELECT X'';",
                new byte[0] );

        [Test]
        public void GetFieldValue_of_byteArray_throws_when_null() =>
            GetX_throws_when_null(
                r => r.GetFieldValue<byte[]>(0));

        [Test]
        public void GetFieldValue_of_DateTime_works() =>
            GetFieldValue_works(
                "SELECT '2014-04-15 11:58:13';",
                new DateTime(2014, 4, 15, 11, 58, 13));

        [Test]
        public void GetFieldValue_of_DateTimeOffset_works() =>
            GetFieldValue_works(
                "SELECT '2014-04-15 11:58:13-08:00';",
                new DateTimeOffset(2014, 4, 15, 11, 58, 13, new TimeSpan(-8, 0, 0)));

        [Test]
        public void GetFieldValue_of_DBNull_works() =>
            GetFieldValue_works(
                "SELECT NULL;",
                DBNull.Value);

        [Test]
        public void GetFieldValue_of_decimal_works() =>
            GetFieldValue_works(
                "SELECT '3.14';",
                3.14m);

        [Test]
        public void GetFieldValue_of_Enum_works() =>
            GetFieldValue_works(
                "SELECT 1;",
                MyEnum.One);

        [Test]
        public void GetFieldValue_of_Guid_works() =>
            GetFieldValue_works(
                "SELECT X'0E7E0DDC5D364849AB9B8CA8056BF93A';",
                new Guid("dc0d7e0e-365d-4948-ab9b-8ca8056bf93a"));

        [Test]
        public void GetFieldValue_of_Nullable_works() =>
            GetFieldValue_works(
                "SELECT 1;",
                (int?)1);

        [Test]
        public void GetFieldValue_of_TimeSpan_works() =>
            GetFieldValue_works(
                "SELECT '12:06:29';",
                new TimeSpan(12, 6, 29));

        [Test]
        public void GetFieldValue_throws_before_read() => X_throws_before_read(r => r.GetFieldValue<DBNull>(0));

        [Test]
        public void GetFieldValue_throws_when_done() => X_throws_when_done(r => r.GetFieldValue<DBNull>(0));

        

        [Test]
        public void GetFieldType_throws_when_ordinal_out_of_range()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var ex = Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetFieldType(1));

                    Assert.AreEqual("ordinal", ex.ParamName);
                    Assert.AreEqual(1, ex.ActualValue);
                }
            }
        }

        [Test]
        public void GetFieldType_throws_when_closed() => X_throws_when_closed(r => r.GetFieldType(0), "GetFieldType");

        [Test]
        public void GetFloat_works() =>
            GetX_works(
                "SELECT 3;",
                r => r.GetFloat(0),
                3f);

        [Test]
        public void GetGuid_works() =>
            GetX_works(
                "SELECT X'0E7E0DDC5D364849AB9B8CA8056BF93A';",
                r => r.GetGuid(0),
                new Guid("dc0d7e0e-365d-4948-ab9b-8ca8056bf93a"));

        [Test]
        public void GetInt16_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetInt16(0),
                (short)1);

        [Test]
        public void GetInt32_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetInt32(0),
                1);

        [Test]
        public void GetInt64_works() =>
            GetX_works(
                "SELECT 1;",
                r => r.GetInt64(0),
                1L);

        [Test]
        public void GetInt64_throws_when_null() =>
            GetX_throws_when_null(
                r => r.GetInt64(0));

        [Test]
        public void GetName_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 AS Id;"))
                {
                    Assert.AreEqual("Id", reader.GetName(0));
                }
            }
        }

        [Test]
        public void GetName_throws_when_ordinal_out_of_range()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var ex = Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetName(1));

                    Assert.AreEqual("ordinal", ex.ParamName);
                    Assert.AreEqual(1, ex.ActualValue);
                }
            }
        }

        [Test]
        public void GetName_throws_when_closed() => X_throws_when_closed(r => r.GetName(0), "GetName");

        [Test]
        public void GetOrdinal_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 AS Id;"))
                {
                    Assert.AreEqual(0, reader.GetOrdinal("Id"));
                }
            }
        }

        [Test]
        public void GetOrdinal_throws_when_out_of_range()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var ex = Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetOrdinal("Name"));
                    Assert.NotNull(ex.Message);
                    Assert.AreEqual("name", ex.ParamName);
                    Assert.AreEqual("Name", ex.ActualValue);
                }
            }
        }

        [Test]
        public void GetString_works_utf8() =>
            GetX_works(
                "SELECT '测试测试测试';",
                r => r.GetString(0),
                "测试测试测试");

        [Test]
        public void GetFieldValue_works_utf8() =>
            GetX_works(
                "SELECT '测试测试测试';",
                r => r.GetFieldValue<string>(0),
                "测试测试测试");

        [Test]
        public void GetValue_to_string_works_utf8() =>
            GetX_works(
                "SELECT '测试测试测试';",
                r => r.GetValue(0) as string,
                "测试测试测试");


        [Test]
        public void GetString_works() =>
            GetX_works(
                "SELECT 'test';",
                r => r.GetString(0),
                "test");

        [Test]
        public void GetString_throws_when_null() =>
            GetX_throws_when_null(
                r => r.GetString(0));

        


        [Test]
        public void GetValue_throws_before_read() => X_throws_before_read(r => r.GetValue(0));

        [Test]
        public void GetValue_throws_when_done() => X_throws_when_done(r => r.GetValue(0));

        [Test]
        public void GetValue_throws_when_closed() => X_throws_when_closed(r => r.GetValue(0), "GetValue");

        [Test]
        public void GetValues_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    // Array may be wider than row
                    var values = new object[2];
                    var result = reader.GetValues(values);

                    Assert.AreEqual(1, result);
                    Assert.AreEqual(1L, values[0]);
                }
            }
        }

        [Test]
        public void GetValues_throws_when_too_narrow()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    var values = new object[0];
                    Assert.Throws<IndexOutOfRangeException>(() => reader.GetValues(values));
                }
            }
        }

        [Test]
        public void HasRows_returns_true_when_rows()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    Assert.True(reader.HasRows);
                }
            }
        }

        [Test]
        public void HasRows_returns_false_when_no_rows()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 WHERE 0 = 1;"))
                {
                    Assert.False(reader.HasRows);
                }
            }
        }

        [Test]
        public void HasRows_works_when_batching()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 WHERE 0 = 1; SELECT 1;"))
                {
                    Assert.False(reader.HasRows);

                    reader.NextResult();

                    Assert.True(reader.HasRows);
                }
            }
        }

        [Test]
        public void IsClosed_returns_false_when_active()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    Assert.False(reader.IsClosed);
                }
            }
        }

        [Test]
        public void IsClosed_returns_true_when_closed()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var reader = connection.ExecuteReader("SELECT 1;");
#if DNX451
                reader.Close();
#else
                ((IDisposable)reader).Dispose();
#endif

                Assert.True(reader.IsClosed);
            }
        }

        [Test]
        public void IsDBNull_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT NULL;"))
                {
                    var hasData = reader.Read();

                    Assert.True(hasData);
                    Assert.True(reader.IsDBNull(0));
                }
            }
        }

        [Test]
        public void IsDBNull_throws_before_read() => X_throws_before_read(r => r.IsDBNull(0));

        [Test]
        public void IsDBNull_throws_when_done() => X_throws_when_done(r => r.IsDBNull(0));

        [Test]
        public void IsDBNull_throws_when_closed() => X_throws_when_closed(r => r.IsDBNull(0), "IsDBNull");

        [Test]
        public void Item_by_ordinal_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.AreEqual(1L, reader[0]);
                }
            }
        }

        [Test]
        public void Item_by_name_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 AS Id;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.AreEqual(1L, reader["Id"]);
                }
            }
        }

        [Test]
        public void NextResult_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1; SELECT 2;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);
                    Assert.AreEqual(1L, reader.GetInt64(0));

                    var hasResults = reader.NextResult();
                    Assert.True(hasResults);

                    hasData = reader.Read();
                    Assert.True(hasData);
                    Assert.AreEqual(2L, reader.GetInt64(0));

                    hasResults = reader.NextResult();
                    Assert.False(hasResults);
                }
            }
        }

        [Test]
        public void NextResult_can_be_called_more_than_once()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1;"))
                {
                    var hasResults = reader.NextResult();
                    Assert.False(hasResults);

                    hasResults = reader.NextResult();
                    Assert.False(hasResults);
                }
            }
        }

        [Test]
        public void NextResult_skips_DML_statements()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.ExecuteNonQuery("CREATE TABLE Test(Value);");

                var sql = @"
                    SELECT 1;
                    INSERT INTO Test VALUES(1);
                    SELECT 2;";
                using (var reader = connection.ExecuteReader(sql))
                {
                    var hasResults = reader.NextResult();
                    Assert.True(hasResults);

                    var hasData = reader.Read();
                    Assert.True(hasData);

                    Assert.AreEqual(2L, reader.GetInt64(0));
                }
            }
        }

        [Test]
        public void Read_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT 1 UNION SELECT 2;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);
                    Assert.AreEqual(1L, reader.GetInt64(0));

                    hasData = reader.Read();
                    Assert.True(hasData);
                    Assert.AreEqual(2L, reader.GetInt64(0));

                    hasData = reader.Read();
                    Assert.False(hasData);
                }
            }
        }

        [Test]
        public void Read_throws_when_closed() => X_throws_when_closed(r => r.Read(), "Read");

        [Test]
        public void RecordsAffected_works()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();
                connection.ExecuteNonQuery("CREATE TABLE Test(Value);");

                var reader = connection.ExecuteReader("INSERT INTO Test VALUES(1);");
                ((IDisposable)reader).Dispose();

                Assert.AreEqual(1, reader.RecordsAffected);
            }
        }

        [Test]
        public void RecordsAffected_works_when_no_DDL()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var reader = connection.ExecuteReader("SELECT 1;");
                ((IDisposable)reader).Dispose();

                Assert.AreEqual(-1, reader.RecordsAffected);
            }
        }

        private static void GetX_works<T>(string sql, Func<DbDataReader, T> action, T expected)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader(sql))
                {
                    var hasData = reader.Read();

                    Assert.True(hasData);
                    Assert.AreEqual(expected, action(reader));
                }
            }
        }

        private static void GetX_throws_when_null(Action<DbDataReader> action)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT NULL;"))
                {
                    var hasData = reader.Read();

                    Assert.True(hasData);
                    Assert.Throws<InvalidCastException>(() => action(reader));
                }
            }
        }

        private static void X_throws_before_read(Action<DbDataReader> action)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT NULL;"))
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => action(reader));

                    Assert.AreEqual(Strings.NoData, ex.Message);
                }
            }
        }

        private static void X_throws_when_done(Action<DbDataReader> action)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (var reader = connection.ExecuteReader("SELECT NULL;"))
                {
                    var hasData = reader.Read();
                    Assert.True(hasData);

                    hasData = reader.Read();
                    Assert.False(hasData);

                    var ex = Assert.Throws<InvalidOperationException>(() => action(reader));
                    Assert.AreEqual(Strings.NoData, ex.Message);
                }
            }
        }

        private static void X_throws_when_closed(Action<DbDataReader> action, string operation)
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                var reader = connection.ExecuteReader("SELECT 1;");
                ((IDisposable)reader).Dispose();

                var ex = Assert.Throws<InvalidOperationException>(() => action(reader));
                Assert.AreEqual(Strings.FormatDataReaderClosed(operation), ex.Message);
            }
        }

        private enum MyEnum
        {
            One = 1
        }
    }
}
