// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Generic;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class FastDictionaryTests
    {
        [Test, Ignore]
        public unsafe void CompareSCGAndFastDictionaryWithInts()
        {
            var d = new Dictionary<int, int>();
            var constDic = new Spreads.Collections.Generic.Experimental.FastDictionary<int, int>();
            var kcDic = new FastDictionary<int, int>();

            for (int i = 0; i < 1000; i++)
            {
                d.Add(i, i);
                constDic.Add(i, i);
                kcDic.Add(i, i);
            }

            const int count = 100000;

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                using (Benchmark.Run("Dictionary", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            sum += d[j];
                        }
                    }
                }
                Assert.True(sum > 0);

                var sum1 = 0L;
                using (Benchmark.Run("Constrained Dictionary", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            sum1 += constDic[j];
                        }
                    }
                }
                Assert.True(sum > 0);
                Assert.AreEqual(sum, sum1);

                var sum2 = 0L;
                using (Benchmark.Run("KeyComparer Dictionary", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            sum2 += kcDic[j];
                        }
                    }
                }
                Assert.True(sum > 0);
                Assert.AreEqual(sum, sum2);

            }

            Benchmark.Dump();
        }

        [Test, Ignore]
        public unsafe void CompareSCGAndFastDictionaryWithSymbol()
        {
            var d = new Dictionary<Symbol, int>();
            var fd = new FastDictionary<Symbol, int>();
            var symbols = new Symbol[1000];
            for (int i = 0; i < 1000; i++)
            {
                var s = new Symbol(i.ToString());
                symbols[i] = s;
                d.Add(s, i);
                fd.Add(s, i);
            }

            const int count = 10000;

            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        sum += d[symbols[j]];
                    }
                }
                sw.Stop();
                Console.WriteLine($"Dictionary {sw.ElapsedMilliseconds}");

                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        sum += fd[symbols[j]];
                    }
                }
                sw.Stop();
                Console.WriteLine($"FastDictionary {sw.ElapsedMilliseconds}");
                Assert.True(sum > 0);
            }
        }
    }
}