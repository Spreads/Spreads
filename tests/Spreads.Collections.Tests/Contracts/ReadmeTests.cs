using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Collections.Tests.Contracts {

    [TestFixture]
    public class ReadmeTests {
        private readonly SortedMap<int, int> _upper = new SortedMap<int, int> { { 2, 2 }, { 4, 4 } };
        private readonly SortedMap<int, int> _lower = new SortedMap<int, int> { { 1, 10 }, { 3, 30 }, { 5, 50 } };

        [Test]
        public void ZipNFromLogoAndReadmeIsEmpty()
        {
            var sum = (_upper + _lower);
            Assert.AreEqual(0, sum.Count());
        }

        [Test]
        public void ZipNFromLogoAndReadmeRepeatWorks() {
            var sum = (_upper.Repeat() + _lower);
            Assert.AreEqual(2, sum.Count());
            Assert.AreEqual(32, sum[3]);
            Assert.AreEqual(54, sum[5]);
        }

        [Test]
        public void ZipNFromLogoAndReadmeRepeatFillWorks() {
            var sum = (_upper.Repeat() + _lower.Fill(42));
            Assert.AreEqual(4, sum.Count());
            Assert.AreEqual(44, sum[2]);
            Assert.AreEqual(32, sum[3]);
            Assert.AreEqual(46, sum[4]);
            Assert.AreEqual(54, sum[5]);
        }

    }
}
