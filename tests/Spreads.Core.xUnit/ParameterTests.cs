// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Xunit;
using Spreads.Algorithms.Optimization;
using System;

namespace Spreads.Core.Tests
{
    public class ParameterTests
    {
        [Fact]
        public void ParameterTest()
        {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 2);

            Assert.Equal(5, par.Steps);

            Assert.Equal(0, par[0]);
            Assert.Equal(5, par[1]);
            Assert.Equal(10, par[2]);
            Assert.Equal(15, par[3]);
            Assert.Equal(19, par[4]);

            Assert.True(par.MoveNext());
            Assert.Equal(0, par.Current);

            Assert.True(par.MoveNext());
            Assert.Equal(5, par.Current);

            Assert.True(par.MoveNext());
            Assert.Equal(10, par.Current);

            Assert.True(par.MoveNext());
            Assert.Equal(15, par.Current);

            Assert.True(par.MoveNext());
            Assert.Equal(19, par.Current);
            Assert.Equal(4, par.CurrentPosition);

            Assert.False(par.MoveNext());
            Assert.Equal(19, par.Current);

            var e = par.GetEnumerator();
            for (int j = 0; j < 2; j++)
            {
                Assert.Equal(-1, e.CurrentPosition);
                Assert.True(e.MoveNext());
                Assert.Equal(0, e.Current);

                Assert.True(e.MoveNext());
                Assert.Equal(5, e.Current);
                e.Reset();
            }
            e.Dispose();

            var i = 0;
            foreach (var current in par)
            {
                Assert.Equal(par[i], current);
                i++;
            }

            // foreach and GetAsyncEnumerator didn't affet the current position which is still 4 from above, before 'var e = ..' line
            Assert.Equal(4, par.CurrentPosition);

            par.Reset();

            Assert.True(par.BigMoveNext());
            Assert.Equal(0, par.Current);
            Assert.Equal(0, par.CurrentPosition);

            Assert.True(par.BigMoveNext());
            Assert.Equal(10, par.Current);
            Assert.Equal(2, par.CurrentPosition);

            Assert.True(par.BigMoveNext());
            Assert.Equal(19, par.Current);
            Assert.Equal(4, par.CurrentPosition);

            Assert.False(par.BigMoveNext());
            Assert.Equal(19, par.Current);
            Assert.Equal(4, par.CurrentPosition);
        }

        [Fact]
        public void BigStepGreaterThanRange()
        {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 4);

            Assert.Equal(5, par.Steps);

            Assert.True(par.BigMoveNext());
            Assert.Equal(0, par.Current);
            Assert.Equal(0, par.CurrentPosition);

            Assert.True(par.BigMoveNext());
            Assert.Equal(19, par.Current);
            Assert.Equal(4, par.CurrentPosition);

            Assert.False(par.BigMoveNext());
            Assert.Equal(19, par.Current);
            Assert.Equal(4, par.CurrentPosition);

            Assert.False(par.MoveNext());
        }

        [Fact]
        public void RegionTest()
        {
            // 0, 5, 10, 15, 19
            var par = new Parameter("test", 0, 19, 5, 4);

            var region = par.GetRegion(2, 1);

            Assert.Equal(3, region.Steps);

            Assert.Equal(5, region[0]);
            Assert.Equal(10, region[1]);
            Assert.Equal(15, region[2]);

            Assert.True(region.MoveNext());

            Assert.Equal(0, region.CurrentPosition);

            // GridPosition position is measured from the original
            Assert.Equal(1, region.GridPosition);

            var region2 = region.GetRegion(1, 0);
            Assert.Equal(1, region2.Steps);

            Assert.True(region2.MoveNext());
            Assert.Equal(0, region2.CurrentPosition);
            Assert.Equal(2, region2.GridPosition);
        }

        [Fact]
        public void LinearAddressTest()
        {
            var par1 = new Parameter("par1", 0, 4, 1);
            var par2 = new Parameter("par2", 0, 4, 1);
            var pars = new Parameters( par1, par2 );
            //var pars = new Parameters();

            pars[0].CurrentPosition = 1;
            pars[1].CurrentPosition = 0;

            Assert.Equal(5, pars.LinearAddress());

            pars[1].CurrentPosition = 1;

            Assert.Equal(6, pars.LinearAddress());

            pars[0].CurrentPosition = 4;
            pars[1].CurrentPosition = 3;
            Assert.Equal(23, pars.LinearAddress());

            var pars2 = pars.SetPositionsFromLinearAddress(23);

            Assert.Equal(4, pars2[0].CurrentPosition);
            Assert.Equal(3, pars2[1].CurrentPosition);
        }

        [Fact]
        public void DynamicAccessToParameters()
        {
            var par1 = new Parameter("Par1", 0, 4, 1);
            var par2 = new Parameter("par2", 0, 4, 1);
            var pars = new[] { par1, par2 };
            dynamic parameters = new Parameters(pars);

            pars[0].CurrentPosition = 1;
            pars[1].CurrentPosition = 2;
            var p1 = (int)parameters.par1;
            Assert.Equal(1, p1);
            Assert.Equal(2, parameters.Par2);
        }

        [Fact]
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