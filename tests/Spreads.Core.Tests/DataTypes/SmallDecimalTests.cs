// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Globalization;
using Shouldly;

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
        public void MaxMinValuesHaveZeroScale()
        {
            SmallDecimal.MaxValue.Scale.ShouldBe((uint)0);
            SmallDecimal.MinValue.Scale.ShouldBe((uint)0);
            SmallDecimal.MaxValue.Decimals.ShouldBe(0);
            SmallDecimal.MinValue.Decimals.ShouldBe(0);

            // Hardcoded 58 to catch future changes in a test: just do not change 58! It's OK. And we have 2 values left (Scale 29/30).
            SmallDecimal.MaxValue.Mantissa.ShouldBe((1UL << 58) - 1);
            SmallDecimal.MinValue.Mantissa.ShouldBe((1UL << 58) - 1);
        }

        [Test]
        public void SubtractDecimalPrice()
        {
            var first = new SmallDecimal(12345.6M);
            var fd = (decimal)first;

            fd.ShouldBe(12345.6M);

            var second = new SmallDecimal(12340.6M);
            var sd = (decimal)second;

            sd.ShouldBe(12340.6M);

            var delta = second - first;
            var dd = (decimal)delta;

            var expectedDelta = 12340.6M - 12345.6M;
            dd.ShouldBe(expectedDelta);

            Console.WriteLine(delta);
        }

        [Test]
        public void SubtractDecimalPrice2()
        {
            var f = 123.45M;
            var s = 1.00000000000155M;

            var first = new SmallDecimal(f);
            var fd = (decimal)first;

            fd.ShouldBe(f);

            var second = new SmallDecimal(s);
            var sd = (decimal)second;

            sd.ShouldBe(s);

            var delta = first - second;
            var dd = (decimal)delta;

            var expectedDelta = f - s;
            dd.ShouldBe(expectedDelta);

            Console.WriteLine(delta);
        }

        [Test]
        public void SubtractDoublePrice()
        {
            var first = (SmallDecimal)(12345.6);
            var fd = (double)first;

            fd.ShouldBe(12345.6);

            var second = (SmallDecimal)(12340.6);
            var sd = (double)second;

            sd.ShouldBe(12340.6);

            var delta = second - first;
            var dd = (double)delta;

            var expectedDelta = 12340.6 - 12345.6;
            expectedDelta.ShouldBe(dd);

            Console.WriteLine(delta);
        }

        [Test]
        public void Negate()
        {
            var first = (SmallDecimal)(12345.6);
            var second = -first;
            ((decimal)second).ShouldBe(-(decimal)first);
        }

        [Test, Explicit("long running")]
        public void ConvertToDoubleDynamic()
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

                sum.ShouldBePositive();
            }

            Benchmark.Dump();
        }

        [Test]
        public void WorksWithNegative()
        {
            var sd = new SmallDecimal(-1);
            ((int)(decimal)sd).ShouldBe(-1);

            sd = new SmallDecimal(-1m);
            ((int)(decimal)sd).ShouldBe(-1);

            sd = (SmallDecimal)(-1.0);
            ((int)(decimal)sd).ShouldBe(-1);
        }

        [Test]
        public void ThrowsOnLargeSmallValues()
        {
            Assert.Throws<OverflowException>(() =>
            {
                var _ = new SmallDecimal(long.MaxValue);
            });

            Assert.Throws<OverflowException>(() =>
            {
                var _ = new SmallDecimal(long.MinValue);
            });

            Assert.Throws<OverflowException>(() =>
            {
                var _ = new SmallDecimal((decimal)long.MaxValue);
            });

            Assert.Throws<OverflowException>(() =>
            {
                var _ = new SmallDecimal((decimal)long.MinValue);
            });

            Assert.Throws<OverflowException>(() =>
            {
                var _ = (SmallDecimal)((double)long.MaxValue);
            });

            Assert.Throws<OverflowException>(() =>
            {
                var _ = (SmallDecimal)((double)long.MinValue);
            });
        }

        [Test]
        public void DecimalsWork()
        {
            var sd = new SmallDecimal(2.5, 0);

            ((decimal)sd).ShouldBe(2.0M);

            sd = new SmallDecimal(3.5, 0);

            ((decimal)sd).ShouldBe(4.0M);

            sd = new SmallDecimal(3.5M, 0, MidpointRounding.AwayFromZero);

            ((decimal)sd).ShouldBe(4.0M);
#if NETCOREAPP3_0
            sd = new SmallDecimal(3.5M, 0, MidpointRounding.ToZero);

            Assert.AreEqual(3.0M, (decimal)sd);

            sd = new SmallDecimal(3.5M, 16, MidpointRounding.ToZero);

            Assert.AreEqual(3.5M, (decimal)sd);
#endif
            Assert.Throws<ArgumentOutOfRangeException>(() => { sd = new SmallDecimal(3.5M, 29); });

            Assert.Throws<ArgumentOutOfRangeException>(() => { sd = new SmallDecimal(3.5M, -2); });
        }

        [Test, Ignore("wrong impl")]
        // ReSharper disable once InconsistentNaming
        public void IInt64DiffableWorks()
        {
            var sd = (SmallDecimal)123.456;
            var sd1 = KeyComparer<SmallDecimal>.Default.Add(sd, 1);
            ((decimal)sd1).ShouldBe(123.457M);

            var diff = KeyComparer<SmallDecimal>.Default.Diff(sd1, sd);
            diff.ShouldBe(1);
        }

        [Test]
        public void ThrowsWhenNaN()
        {
            var sd = (SmallDecimal)(double.NaN);

            sd.IsNaN.ShouldBeTrue();

            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = sd + 1;
            });
        }

#if BUILTIN_SPAN
        [TestCase("123.45600", 5)]
        [TestCase("0.000001", 6)]
        [TestCase("10.000001", 6)]
        [TestCase("1000000000000.001", 3)]
        [TestCase("-123.45600", 5)]
        [TestCase("-0.000100", 6)]
        [TestCase("-10.000001", 6)]
        [TestCase(" -1000000000000.001 ", 3)]
        [TestCase("2.5e-002", 3)]
        [TestCase("-2.5e-002", 3)]
        public void ParseWithDecimals(string str, int decimals)
        {
            var sd = SmallDecimal.Parse(str);
            sd.Decimals.ShouldBe(decimals);
            decimal value = decimal.Parse(str, NumberStyles.Number | NumberStyles.AllowExponent);
            ((decimal)sd).ShouldBe(value);
        }
#endif

        [TestCase("123.456", 789, 3)]
        [TestCase("123.456", -789, 3)]
        [TestCase("0.0000000000456", -789, 13)]
        [TestCase("288230376151711743", 1, 0)]
        [TestCase("-288230376151711743", 1, 0)]
        [TestCase("288230376151711743", 0, 0)]
        [TestCase("-288230376151711743", 0, 0)]
        [TestCase("128230376151711743", 2, 0)]
        [TestCase("-128230376151711743", 2, 0)]
        [TestCase("12823037.6151711743", 2, 10)]
        [TestCase("-12823037.6151711743", 2, 10)]
        [TestCase("0.0000000000128230376151711743", 2, 28)]
        [TestCase("-0.0000000000128230376151711743", 2, 28)]
        [TestCase("0.0000000000012230376151711743", 20, 28)]
        [TestCase("-0.0000000000012230376151711743", 20, 28)]
        public void MultiplicationByInt(string leftStr, int right, int decimals)
        {
            var left = decimal.Parse(leftStr);
            var leftSd = new SmallDecimal(left);
            var product = leftSd * right;

            ((decimal)product).ShouldBe(left * right);
            product.Decimals.ShouldBe(decimals);
        }

        [Test, Explicit("Bench")]
        public void MultiplicationByIntBench()
        {
            var outerCount = 20;
            var innerCount = 1_000_000;

            SmallDecimal[] sdecs = new SmallDecimal[outerCount];
            Decimal[] decs = new decimal[outerCount];

            sdecs[0] = new SmallDecimal(SmallDecimal.MaxValue.Mantissa / (ulong)(innerCount / 2));
            decs[0] = sdecs[0];
            sdecs[1] = new SmallDecimal(SmallDecimal.MaxValue.Mantissa / (ulong)(innerCount / 2));
            decs[1] = sdecs[1];
            ;

            for (int i = 2; i < outerCount; i++)
            {
                var smallDecimal = new SmallDecimal(Math.Pow(3, i), i);
                if (i % 2 == 0)
                    smallDecimal = -smallDecimal;
                sdecs[i] = smallDecimal;
                decs[i] = smallDecimal;
            }

            for (int r = 0; r < 10; r++)
            {
                Benchmark.Run("MultByInt", () =>
                {
                    for (int s = 0; s < outerCount; s++)
                    {
                        var sd = sdecs[s];
                        var d = decs[s];
                        for (int i = -innerCount / 2; i < innerCount / 2; i++)
                        {
                            var result = sd * i;
                            // comment the check before running a benchmark
                            // var expected = d * i;
                            // if ((decimal)result != expected)
                            //     Assert.Fail();
                        }
                    }
                }, outerCount * innerCount);

                Benchmark.Run("MultDecByInt", () =>
                {
                    for (int s = 0; s < outerCount; s++)
                    {
                        var d = decs[s];
                        for (int i = -innerCount / 2; i < innerCount / 2; i++)
                        {
                            var result = d * i;
                        }
                    }

                }, outerCount * innerCount);
            }

            Benchmark.Dump(opsAggregate:Benchmark.OpsAggregate.MaxTime);
        }
    }
}
