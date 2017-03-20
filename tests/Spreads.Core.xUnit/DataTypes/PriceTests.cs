// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Spreads.DataTypes;

namespace Spreads.Core.Tests
{
    public class PriceTests
    {
        [Fact]
        public void CouldAddDecimalPrice()
        {
            var first = new Price(12345.6M);
            var fd = (decimal)first;

            Assert.Equal(12345.6M, fd);

            var second = new Price(12340.6M);
            var sd = (decimal)second;

            Assert.Equal(12340.6M, sd);

            var delta = second - first;
            var dd = (decimal)delta;

            var expectedDelta = 12340.6M - 12345.6M;
            Assert.Equal(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Fact]
        public void CouldAddDoublePrice()
        {
            var first = new Price(12345.6);
            var fd = (double)first;

            Assert.Equal(12345.6, fd);

            var second = new Price(12340.6);
            var sd = (double)second;

            Assert.Equal(12340.6, sd);

            var delta = second - first;
            var dd = (double)delta;

            var expectedDelta = 12340.6 - 12345.6;
            Assert.Equal(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Fact]
        public void CouldNegatePrice()
        {
            var first = new Price(12345.6);
            var second = -first;
            Assert.Equal(-(decimal)first, (decimal)second);
        }
    }
}