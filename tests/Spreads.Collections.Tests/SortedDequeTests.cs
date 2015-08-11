using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;
using Spreads.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace Spreads.Collections.Tests {

    [TestFixture]
    public class SortedDequeTests {

        [Test]
        public void CouldRemoveFirstAddInTheMiddle()
        {
            var sd = new SortedDeque<int>();
            sd.Add(1);
            sd.Add(3);
            sd.Add(5);
            sd.Add(7);

            Assert.AreEqual(sd.First, 1);
            Assert.AreEqual(sd.Last, 7);

            var fst = sd.RemoveFirst();
            Assert.AreEqual(sd.First, 3);
            sd.Add(4);
            Assert.AreEqual(1, sd.IndexOfElement(4));
            Assert.AreEqual(2, sd.IndexOfElement(5));
            Assert.AreEqual(3, sd.IndexOfElement(7));

            var last = sd.RemoveLast();
            sd.Add(8);
            Assert.AreEqual(1, sd.IndexOfElement(4));
            Assert.AreEqual(2, sd.IndexOfElement(5));
            Assert.AreEqual(3, sd.IndexOfElement(8));
        }

        [Test]
        public void CouldAddBehindInitialCapacity()
        {
            var sd = new SortedDeque<int>();
            for (int i = 0; i < 4; i++)
            {
                sd.Add(i);
            }

            for (int i = 0; i < 4; i++) {
                Assert.AreEqual(i, sd.IndexOfElement(i));
            }
        }

    }
}
