using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Storage;


namespace Spreads.Core.Tests {



    [TestFixture]
    public class DataTypesTest {


      

        [Test]
        public void SymbolWorks()
        {
            var text = "test symbol 16 b";
            var symbol = new Symbol(text);
            var symbol2 = new Symbol(text);
            var symbol3 = new Symbol("different symbol");
            Assert.AreEqual(text, symbol.ToString());
            Assert.AreEqual(symbol, symbol2);
            Assert.AreNotEqual(symbol3, symbol);
            Assert.IsTrue(symbol == symbol2);
            Assert.IsTrue(symbol3 != symbol2);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var symbol4 = new Symbol("this symbol is very long");
            });

        }

        [Test]
        public void PriceWorks()
        {
            var price = new Price(42.31400M, 4);
            var price2 = new Price(42.31400M, 5);
            var price3 = new Price(-42.31400M, 4);
            var asString = price.ToString();
            Assert.AreEqual("42.3140", asString);

            var sum = price + price;
            Assert.AreEqual("84.6280", sum.ToString());
            var sum2 = price + price2;
            Assert.AreEqual("84.62800", sum2.ToString());
            Assert.AreEqual(sum2.Exponent, 5);
            var sum3 = price + price3;
            Assert.AreEqual("0.0000", sum3.ToString());
            var sum4 = sum3 - price;
            Assert.AreEqual(price3, sum4);
            Assert.AreEqual("-42.3140", sum4.ToString());
        }

    }
}
