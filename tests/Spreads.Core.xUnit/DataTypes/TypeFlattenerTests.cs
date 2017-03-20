using Xunit;
using Spreads.DataTypes;
using System;
using System.Runtime.Serialization;

namespace Spreads.Tests.DataTypes
{
    public class TypeFlattenerTests
    {
        

        [Fact]
        public void CouldFlattenScalar()
        {
            var price = new Price(123.45);
            var flattener = new TypeFlattenner(price.GetType());
            object[] values = null;

            flattener.Flatten(price, ref values);

            Console.WriteLine($"Columns: {flattener.Columns[0]} ");

            Assert.Equal("Price", flattener.Columns[0]);
            Assert.Equal(new Price(123.45), values[0]);
        }

        [Fact]
        public void CouldFlattenDouble()
        {
            var dbl = 123.45;
            var flattener = new TypeFlattenner(dbl.GetType());
            object[] values = null;

            flattener.Flatten(dbl, ref values);

            Console.WriteLine($"Columns: {flattener.Columns[0]} ");

            Assert.Equal("Double", flattener.Columns[0]);
            Assert.Equal(123.45, values[0]);
        }

        public class TestType
        {
            public int Number { get; set; }
            public string Text { get; set; }
            public Price Price { get; set; }
        }

        public class TestTypeWithPartialOrder
        {
            [DataMember]
            public int Number { get; set; }

            [DataMember(Order = 1)]
            public string Text { get; set; }

            [DataMember]
            public Price Price { get; set; }
        }

        [Fact]
        public void CouldFlattenCustomType()
        {
            var value = new TestType { Number = 42, Text = "foo", Price = new Price(123.45) };
            var flattener = new TypeFlattenner(value.GetType());
            object[] values = null;

            flattener.Flatten(value, ref values);

            Console.WriteLine($"Columns: {flattener.Columns[0]} - {flattener.Columns[1]} - {flattener.Columns[2]}");

            Assert.Equal("Number", flattener.Columns[0]);
            Assert.Equal("Price", flattener.Columns[1]);
            Assert.Equal("Text", flattener.Columns[2]);

            Assert.Equal(42, values[0]);
            Assert.Equal(new Price(123.45), values[1]);
            Assert.Equal("foo", values[2]);
        }

        [Fact]
        public void CouldFlattenCustomTypeWithPartialOrder()
        {
            var value = new TestTypeWithPartialOrder { Number = 42, Text = "foo", Price = new Price(123.45) };
            var flattener = new TypeFlattenner(value.GetType());
            object[] values = null;

            flattener.Flatten(value, ref values);

            Console.WriteLine($"Columns: {flattener.Columns[0]} - {flattener.Columns[1]} - {flattener.Columns[2]}");

            Assert.Equal("Text", flattener.Columns[0]);
            Assert.Equal("Number", flattener.Columns[1]);
            Assert.Equal("Price", flattener.Columns[2]);

            Assert.Equal("foo", values[0]);
            Assert.Equal(42, values[1]);
            Assert.Equal(new Price(123.45), values[2]);
        }
    }
}