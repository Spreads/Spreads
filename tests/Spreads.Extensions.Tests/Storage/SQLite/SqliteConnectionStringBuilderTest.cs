// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Microsoft.Data.Sqlite
{
    [TestFixture]
    public class SqliteConnectionStringBuilderTest
    {
        [SetUp]
        public void Init() {
            var bs = Bootstrap.Bootstrapper.Instance;
        }
        [Test]
        public void Ctor_parses_options()
        {
            var builder = new SqliteConnectionStringBuilder("Data Source=test.db");

            Assert.AreEqual("test.db", builder.DataSource);
        }

        [Test]
        public void Ctor_parses_SharedCache()
        {
            Assert.AreEqual(SqliteCacheMode.Private, new SqliteConnectionStringBuilder("Cache=Private").Cache);
            Assert.AreEqual(SqliteCacheMode.Shared, new SqliteConnectionStringBuilder("Cache=Shared").Cache);
        }

        [Test]
        public void Ctor_parses_mode()
        {
            var builder = new SqliteConnectionStringBuilder("Mode=Memory");

            Assert.AreEqual(SqliteOpenMode.Memory, builder.Mode);
        }

        [Test]
        public void Filename_is_alias_for_DataSource()
        {
            var builder = new SqliteConnectionStringBuilder("Filename=inline.db");
            Assert.AreEqual("inline.db", builder.DataSource);
        }

        [Test]
        public void It_takes_last_alias_specified()
        {
            var builder = new SqliteConnectionStringBuilder("Filename=ignore me.db; Data Source=and me too.db; DataSource=this_one.db");

            Assert.AreEqual("this_one.db", builder.DataSource);
        }

        [Test]
        public void DataSource_works()
        {
            var builder = new SqliteConnectionStringBuilder();

            builder.DataSource = "test.db";

            Assert.AreEqual("test.db", builder.DataSource);
        }

        [Test]
        public void DataSource_defaults_to_empty()
        {
            Assert.IsEmpty(new SqliteConnectionStringBuilder().DataSource);
        }

        [Test]
        public void Mode_works()
        {
            var builder = new SqliteConnectionStringBuilder();

            builder.Mode = SqliteOpenMode.Memory;

            Assert.AreEqual(SqliteOpenMode.Memory, builder.Mode);
        }

        [Test]
        public void Mode_defaults_to_ReadWriteCreate()
            => Assert.AreEqual(SqliteOpenMode.ReadWriteCreate, new SqliteConnectionStringBuilder().Mode);

        [Test]
        public void Cache_defaults()
        {
            Assert.AreEqual(SqliteCacheMode.Default, new SqliteConnectionStringBuilder().Cache);
        }

        [Test]
        public void Keys_works()
        {
            var keys = (ICollection<string>)new SqliteConnectionStringBuilder().Keys;

            Assert.True(keys.IsReadOnly);
            Assert.AreEqual(3, keys.Count);
            //Assert.Contains("Data Source", keys);
            //Assert.Contains("Mode", keys);
            //Assert.Contains("Cache", keys);
        }

        [Test]
        public void Values_works()
        {
            var values = (ICollection<object>)new SqliteConnectionStringBuilder().Values;

            Assert.True(values.IsReadOnly);
            Assert.AreEqual(3, values.Count);
        }

        //[Test]
        //public void Item_validates_argument()
        //{
        //    var ex = Assert.Throws<ArgumentException>(() => new SqliteConnectionStringBuilder()["Invalid"]);
        //    Assert.AreEqual(Strings.FormatKeywordNotSupported("Invalid"), ex.Message);

        //    ex = Assert.Throws<ArgumentException>(() => new SqliteConnectionStringBuilder()["Invalid"] = 0);
        //    Assert.AreEqual(Strings.FormatKeywordNotSupported("Invalid"), ex.Message);
        //}

        [Test]
        public void Item_resets_value_when_null()
        {
            var builder = new SqliteConnectionStringBuilder();
            builder.DataSource = "test.db";

            builder["Data Source"] = null;

            Assert.IsEmpty(builder.DataSource);
        }

        [Test]
        public void Item_gets_value()
        {
            var builder = new SqliteConnectionStringBuilder();
            builder.DataSource = "test.db";

            Assert.AreEqual("test.db", builder["Data Source"]);
        }

        [Test]
        public void Item_sets_value()
        {
            var builder = new SqliteConnectionStringBuilder();

            builder["Data Source"] = "test.db";

            Assert.AreEqual("test.db", builder.DataSource);
        }

       

        [Test]
        public void Clear_resets_everything()
        {
            var builder = new SqliteConnectionStringBuilder("Data Source=test.db;Mode=Memory;Cache=Shared");

            builder.Clear();

            Assert.IsEmpty(builder.DataSource);
            Assert.AreEqual(SqliteOpenMode.ReadWriteCreate, builder.Mode);
            Assert.AreEqual(SqliteCacheMode.Default, builder.Cache);
        }

        [Test]
        public void ContainsKey_returns_true_when_exists()
        {
            Assert.True(new SqliteConnectionStringBuilder().ContainsKey("Data Source"));
        }

        [Test]
        public void ContainsKey_returns_false_when_not_exists()
        {
            Assert.False(new SqliteConnectionStringBuilder().ContainsKey("Invalid"));
        }

        [Test]
        public void Remove_returns_false_when_not_exists()
        {
            Assert.False(new SqliteConnectionStringBuilder().Remove("Invalid"));
        }

        [Test]
        public void Remove_resets_option()
        {
            var builder = new SqliteConnectionStringBuilder("Data Source=test.db");

            var removed = builder.Remove("Data Source");

            Assert.True(removed);
            Assert.IsEmpty(builder.DataSource);
        }

        [Test]
        public void ShouldSerialize_returns_false_when_not_exists()
        {
            Assert.False(new SqliteConnectionStringBuilder().ShouldSerialize("Invalid"));
        }

        

        [Test]
        public void TryGetValue_returns_false_when_not_exists()
        {
            object value;
            var retrieved = new SqliteConnectionStringBuilder().TryGetValue("Invalid", out value);

            Assert.False(retrieved);
            Assert.Null(value);
        }

        [Test]
        public void TryGetValue_returns_true_when_exists()
        {
            var builder = new SqliteConnectionStringBuilder("Data Source=test.db");

            object value;
            var retrieved = builder.TryGetValue("Data Source", out value);

            Assert.True(retrieved);
            Assert.AreEqual("test.db", value);
        }

        [Test]
        public void ToString_builds_string()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = "test.db",
                Cache = SqliteCacheMode.Shared,
                Mode = SqliteOpenMode.Memory
            };

            Assert.AreEqual("Data Source=test.db;Mode=Memory;Cache=Shared", builder.ToString());
        }

        [Test]
        public void ToString_builds_minimal_string()
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = "test.db"
            };

            Assert.AreEqual("Data Source=test.db", builder.ToString());
        }
    }
}
