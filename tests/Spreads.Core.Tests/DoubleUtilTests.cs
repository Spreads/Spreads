// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class DoubleUtilTests
    {
        [Test, Ignore]
        public void CompareOppositeSignSpeed()
        {
            var doubles = new double[10000000];
            var rng = new System.Random();
            doubles[0] = 0.0;
            doubles[1] = 0.0;
            for (int i = 2; i < doubles.Length; i++)
            {
                doubles[i] = rng.NextDouble() - 0.5;
            }

            for (int i = 1; i < doubles.Length; i++)
            {

                var isOpposite = doubles[i].IsOppositeSign(doubles[i - 1]);
                var isOpposite2 = doubles[i].IsOppositeSignBitwise(doubles[i - 1]);
                var isOpposite3 = doubles[i].IsOppositeSignNaive(doubles[i - 1]);
                if ((isOpposite != isOpposite2) | (isOpposite != isOpposite3))
                {
                    Assert.Fail($"Wrong opposites: MS {isOpposite} vs BW {isOpposite2} for {doubles[i]} and {doubles[i - 1]}");
                }
            }

            var count1 = 0L;
            var count2 = 0L;
            var count3 = 0L;
            var sw = new Stopwatch();
            for (int rr = 0; rr < 10; rr++)
            {
                sw.Restart();
                for (int r = 0; r < 10; r++)
                {
                    for (int i = 1; i < doubles.Length; i++)
                    {
                        var isOpposite = doubles[i].IsOppositeSign(doubles[i - 1]);
                        if (isOpposite) count1++;
                    }
                }
                sw.Stop();

                Console.WriteLine($"Elapsed Product < 0: {sw.ElapsedMilliseconds}");

                sw.Restart();
                for (int r = 0; r < 10; r++)
                {
                    for (int i = 1; i < doubles.Length; i++)
                    {
                        var isOpposite = doubles[i].IsOppositeSignNaive(doubles[i - 1]);
                        if (isOpposite) count2++;
                    }
                }
                sw.Stop();

                Console.WriteLine($"Elapsed Math.Sign: {sw.ElapsedMilliseconds}");

                sw.Restart();
                for (int r = 0; r < 10; r++)
                {
                    for (int i = 1; i < doubles.Length; i++)
                    {
                        var isOpposite = doubles[i].IsOppositeSign(doubles[i - 1]);
                        if (isOpposite) count3++;
                    }
                }
                sw.Stop();

                Console.WriteLine($"Elapsed BitWise: {sw.ElapsedMilliseconds}");
            }
            Console.WriteLine(count1);
            Console.WriteLine(count2);
            Assert.AreEqual(count1, count2);
        }
    }
}