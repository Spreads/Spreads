// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms.Optimization;
using System;
using Spreads.Collections.Generic;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class ParameterTests
    {
        [Test]
        public void ParameterTest()
        {
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
            for (int j = 0; j < 2; j++)
            {
                Assert.AreEqual(-1, e.CurrentPosition);
                Assert.IsTrue(e.MoveNext());
                Assert.AreEqual(0, e.Current);

                Assert.IsTrue(e.MoveNext());
                Assert.AreEqual(5, e.Current);
                e.Reset();
            }
            e.Dispose();

            var i = 0;
            foreach (var current in par)
            {
                Assert.AreEqual(par[i], current);
                i++;
            }

            // foreach and GetAsyncEnumerator didn't affet the current position which is still 4 from above, before 'var e = ..' line
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
        public void BigStepGreaterThanRange()
        {
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
        public void RegionTest()
        {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 4);

            var region = par.GetRegion(2, 1);

            Assert.AreEqual(3, region.Steps);

            Assert.AreEqual(5, region[0]);
            Assert.AreEqual(10, region[1]);
            Assert.AreEqual(15, region[2]);

            Assert.IsTrue(region.MoveNext());

            Assert.AreEqual(0, region.CurrentPosition);

            // GridPosition position is measured from the original
            Assert.AreEqual(1, region.GridPosition);

            var region2 = region.GetRegion(1, 0);
            Assert.AreEqual(1, region2.Steps);

            Assert.IsTrue(region2.MoveNext());
            Assert.AreEqual(0, region2.CurrentPosition);
            Assert.AreEqual(2, region2.GridPosition);
        }

        [Test]
        public void LinearAddressTest()
        {
            var par1 = new Parameter("par1", 0, 4, 1);
            var par2 = new Parameter("par2", 0, 4, 1);
            var pars = new RefList<Parameter>();
            pars.Add(par1);
            pars.Add(par2);
            //var pars = new Parameters();

            pars.Ref(0).CurrentPosition = 1;
            pars.Ref(1).CurrentPosition = 0;

            Assert.AreEqual(5, pars.LinearAddress());

            pars.Ref(1).CurrentPosition = 1;

            Assert.AreEqual(6, pars.LinearAddress());

            pars.Ref(0).CurrentPosition = 4;
            pars.Ref(1).CurrentPosition = 3;
            Assert.AreEqual(23, pars.LinearAddress());

            var pars2 = pars.SetPositionsFromLinearAddress(23);

            Assert.AreEqual(4, pars2[0].CurrentPosition);
            Assert.AreEqual(3, pars2[1].CurrentPosition);
        }

        [Test]
        public void DynamicAccessToParameters()
        {
            var par1 = new Parameter("Par1", 0, 4, 1);
            var par2 = new Parameter("par2", 0, 4, 1);
            var pars = new[] { par1, par2 };
            dynamic parameters = new Parameters(pars);

            pars[0].CurrentPosition = 1;
            pars[1].CurrentPosition = 2;
            var p1 = (int)parameters.par1;
            Assert.AreEqual(1, p1);
            Assert.AreEqual(2, parameters.Par2);
        }

        [Test]
        public void NonUniqueParameterCodesThrow()
        {
            var par1 = new Parameter("Par1", 0, 4, 1);
            var par2 = new Parameter("par1", 0, 4, 1);
            var pars = new[] { par1, par2 };
            Assert.Throws<ArgumentException>(() =>
            {
                var parameters = new Parameters(pars);
            });
        }
    }
}