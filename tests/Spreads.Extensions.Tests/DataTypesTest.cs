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



    }
}
