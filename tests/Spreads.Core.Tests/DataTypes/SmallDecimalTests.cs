// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.DataTypes
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SmallDecimalTests
    {
        [Test, Explicit("output")]
        public void PrintDecimalLayout()
        {
            decimal d = new decimal(0.1);
            d = Math.Round(d, 5);

            var dc = *(SmallDecimal.DecCalc*)&d;
            Console.WriteLine(dc.ToString());
        }

        [Test]
        public void CouldSubtractDecimalPrice()
        {
            var first = new SmallDecimal(12345.6M);
            var fd = (decimal)first;

            Assert.AreEqual(12345.6M, fd);

            var second = new SmallDecimal(12340.6M);
            var sd = (decimal)second;

            Assert.AreEqual(12340.6M, sd);

            var delta = second - first;
            var dd = (decimal)delta;

            var expectedDelta = 12340.6M - 12345.6M;
            Assert.AreEqual(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Test]
        public void CouldSubtractDoublePrice()
        {
            var first = (SmallDecimal)(12345.6);
            var fd = (double)first;

            Assert.AreEqual(12345.6, fd);

            var second = (SmallDecimal)(12340.6);
            var sd = (double)second;

            Assert.AreEqual(12340.6, sd);

            var delta = second - first;
            var dd = (double)delta;

            var expectedDelta = 12340.6 - 12345.6;
            Assert.AreEqual(expectedDelta, dd);

            Console.WriteLine(delta);
        }

        [Test]
        public void CouldNegate()
        {
            var first = (SmallDecimal)(12345.6);
            var second = -first;
            Assert.AreEqual(-(decimal)first, (decimal)second);
        }

        [Test, Explicit("long running")]
        public void CouldConvertToDoubleDynamic()
        {
#if DEBUG
            var count = 10_000;
#else
            var count = 10_000_000;
#endif
            for (int r = 0; r < 20; r++)
            {
                var sum = 0.0;
                using (Benchmark.Run("Dynamic cast", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dyn = (dynamic)price;
                        // ReSharper disable once PossibleInvalidCastException
                        var dbl = (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Convert nobox", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dbl = Convert.ToDouble(price);
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Convert box", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dbl = Convert.ToDouble((object)price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("DoubleUtil nobox", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dbl = DoubleUtil.GetDouble(price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("DoubleUtil box", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dbl = DoubleUtil.GetDouble((object)price); // (double)dyn;
                        sum += dbl;
                    }
                }

                using (Benchmark.Run("Direct", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var price = (SmallDecimal)((double)i);
                        var dbl = (double)price;
                        sum += dbl;
                    }
                }

                Assert.IsTrue(sum > 0);
            }
            Benchmark.Dump();
        }

        [Test]
        public void WorksWithNegative()
        {
            var sd = new SmallDecimal(-1);
            Assert.AreEqual(-1, (int)(decimal)sd);

            sd = new SmallDecimal(-1m);
            Assert.AreEqual(-1, (int)(decimal)sd);

            sd = (SmallDecimal)(-1.0);
            Assert.AreEqual(-1, (int)(decimal)sd);
        }

        [Test]
        public void ThrowsOnLargeSmallValues()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new SmallDecimal(long.MaxValue);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new SmallDecimal(long.MinValue);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new SmallDecimal((decimal)long.MaxValue);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var _ = new SmallDecimal((decimal)long.MinValue);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var _ = (SmallDecimal)((double)long.MaxValue);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var _ = (SmallDecimal)((double)long.MinValue);
            });
        }

        [Test]
        public void DecimalsWork()
        {
            var sd = new SmallDecimal(2.5, 0);

            Assert.AreEqual(2.0M, (decimal)sd);

            sd = new SmallDecimal(3.5, 0);

            Assert.AreEqual(4.0M, (decimal)sd);

            sd = new SmallDecimal(3.5M, 0, MidpointRounding.AwayFromZero);

            Assert.AreEqual(4.0M, (decimal)sd);
#if NETCOREAPP3_0
            sd = new SmallDecimal(3.5M, 0, MidpointRounding.ToZero);

            Assert.AreEqual(3.0M, (decimal)sd);

            sd = new SmallDecimal(3.5M, 16, MidpointRounding.ToZero);

            Assert.AreEqual(3.5M, (decimal)sd);
#endif
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                sd = new SmallDecimal(3.5M, 17);
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                sd = new SmallDecimal(3.5M, -2);
            });
        }

       

        [Test, Ignore("wrong impl")]
        // ReSharper disable once InconsistentNaming
        public void IInt64DiffableWorks()
        {
            var sd = (SmallDecimal)(123.456);
            var sd1 = KeyComparer<SmallDecimal>.Default.Add(sd, 1);
            Assert.AreEqual(123.457, (decimal)sd1);

            var diff = KeyComparer<SmallDecimal>.Default.Diff(sd1, sd);
            Assert.AreEqual(1, diff);
        }

        [Test]
        public void NaNThrows()
        {
            var sd = (SmallDecimal)(double.NaN);

            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = sd + 1;
            });
        }
    }
}
