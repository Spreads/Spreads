// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Spreads.Algorithms.Optimization;

namespace Spreads.Core.Tests {


    [TestFixture]
    public class ParameterTests {

        [Test]
        public void ParameterTest() {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 2);

            Assert.AreEqual(5, par.Steps);

            Assert.AreEqual(0, par[0]);
            Assert.AreEqual(5, par[1]);
            Assert.AreEqual(10, par[2]);
            Assert.AreEqual(15, par[3]);
            Assert.AreEqual(19, par[4]);

            Assert.IsTrue(par.MoveNext());
            Assert.AreEqual(0, par.Current);

            Assert.IsTrue(par.MoveNext());
            Assert.AreEqual(5, par.Current);

            Assert.IsTrue(par.MoveNext());
            Assert.AreEqual(10, par.Current);

            Assert.IsTrue(par.MoveNext());
            Assert.AreEqual(15, par.Current);

            Assert.IsTrue(par.MoveNext());
            Assert.AreEqual(19, par.Current);
            Assert.AreEqual(4, par.CurrentPosition);

            Assert.IsFalse(par.MoveNext());
            Assert.AreEqual(19, par.Current);

            var e = par.GetEnumerator();
            for (int j = 0; j < 2; j++) {
                Assert.AreEqual(-1, e.CurrentPosition);
                Assert.IsTrue(e.MoveNext());
                Assert.AreEqual(0, e.Current);

                Assert.IsTrue(e.MoveNext());
                Assert.AreEqual(5, e.Current);
                e.Reset();
            }
            e.Dispose();

            var i = 0;
            foreach (var current in par) {
                Assert.AreEqual(par[i], current);
                i++;
            }

            // foreach and GetEnumerator didn't affet the current position which is still 4 from above, before 'var e = ..' line
            Assert.AreEqual(4, par.CurrentPosition);

            par.Reset();

            Assert.IsTrue(par.BigMoveNext());
            Assert.AreEqual(0, par.Current);
            Assert.AreEqual(0, par.CurrentPosition);

            Assert.IsTrue(par.BigMoveNext());
            Assert.AreEqual(10, par.Current);
            Assert.AreEqual(2, par.CurrentPosition);

            Assert.IsTrue(par.BigMoveNext());
            Assert.AreEqual(19, par.Current);
            Assert.AreEqual(4, par.CurrentPosition);

            Assert.IsFalse(par.BigMoveNext());
            Assert.AreEqual(19, par.Current);
            Assert.AreEqual(4, par.CurrentPosition);

        }

        [Test]
        public void BigStepGreaterThanRange() {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 4);

            Assert.AreEqual(5, par.Steps);

            Assert.IsTrue(par.BigMoveNext());
            Assert.AreEqual(0, par.Current);
            Assert.AreEqual(0, par.CurrentPosition);

            Assert.IsTrue(par.BigMoveNext());
            Assert.AreEqual(19, par.Current);
            Assert.AreEqual(4, par.CurrentPosition);


            Assert.IsFalse(par.BigMoveNext());
            Assert.AreEqual(19, par.Current);
            Assert.AreEqual(4, par.CurrentPosition);

            Assert.IsFalse(par.MoveNext());
        }


        [Test]
        public void RegionTest() {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 4);

            var region = par.GetRegion(2, 1);

            Assert.AreEqual(3, region.Steps);

            Assert.AreEqual(5, region[0]);
            Assert.AreEqual(10, region[1]);
            Assert.AreEqual(15, region[2]);

        }


    }
}
