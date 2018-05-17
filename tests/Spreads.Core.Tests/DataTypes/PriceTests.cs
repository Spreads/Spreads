// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Utils;

namespace Spreads.Tests.DataTypes
{
    [TestFixture]
    public class PriceTests
    {
        [Test]
        public void CouldAddDecimalPrice()
        {
            var first = new Price(12345.6M);
            var fd = (decimal)first;

            Assert.AreEqual(12345.6M, fd);

            var second = new Price(12340.6M);
            var sd = (decimal)second;

            Assert.AreEqual(12340.6M, sd);

            var delta = second - first;
            var dd = (decimal)delta;

            var expectedDelta = 12340.6M - 12345.6M;
            Assert.AreEqual(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Test]
        public void CouldAddDoublePrice()
        {
            var first = new Price(12345.6);
            var fd = (double)first;

            Assert.AreEqual(12345.6, fd);

            var second = new Price(12340.6);
            var sd = (double)second;

            Assert.AreEqual(12340.6, sd);

            var delta = second - first;
            var dd = (double)delta;

            var expectedDelta = 12340.6 - 12345.6;
            Assert.AreEqual(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Test]
        public void CouldNegatePrice()
        {
            var first = new Price(12345.6);
            var second = -first;
            Assert.AreEqual(-(decimal)first, (decimal)second);
        }

        [Test, Ignore("long running")]
        public void CouldConvertToDoubleDynamic()
        {
            var count = 10_000_000;
            for (int r = 0; r < 20; r++)
            {
                var sum = 0.0;
                using (Benchmark.Run("Dynamic cast", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dyn = (dynamic)price;
                        var dbl = (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Convert nobox", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dbl = Convert.ToDouble(price);
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Convert box", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dbl = Convert.ToDouble((object)price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("DoubleUtil nobox", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dbl = DoubleUtil.GetDouble(price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("DoubleUtil box", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dbl = DoubleUtil.GetDouble((object)price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Direct", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = new Price((double)i);
                        var dbl = price.AsDouble;
                        sum += dbl;
                    }
                }

                Assert.IsTrue(sum > 0);
            }
            Benchmark.Dump();
        }
    }
}