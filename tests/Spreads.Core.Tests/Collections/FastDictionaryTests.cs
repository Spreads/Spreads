// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Collections.Generic;
using NUnit.Framework;
using Spreads.Collections.Generic;
using Spreads.DataTypes;
using Spreads.Utils;

namespace Spreads.Tests.Collections
{
    [TestFixture]
    public class FastDictionaryTests
    {
        [Test, Explicit("long running")]
        public void CompareSCGAndFastDictionaryWithInts()
        {
            var dictionary = new Dictionary<long, long>();
            var fastDictionary = new FastDictionary<long, long>();
            var concurrentDictionary = new ConcurrentDictionary<long, long>();

            var length = 10000;

            for (int i = 0; i < length; i++)
            {
                dictionary.Add(i, i);
                fastDictionary.Add(i, i);
                concurrentDictionary[i] = i;
            }

            const int count = 5000;

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                using (Benchmark.Run("Dictionary", count * length))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            sum += dictionary[j];
                        }
                    }
                }
                Assert.True(sum > 0);

                var sum1 = 0L;
                using (Benchmark.Run("FastDictionary", count * length))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            sum1 += fastDictionary[j];
                        }
                    }
                }
                Assert.True(sum1 > 0);
                // Assert.AreEqual(sum, sum1);

                var sum2 = 0L;
                using (Benchmark.Run("ConcurrentDictionary", count * length))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            sum2 += concurrentDictionary[j];
                        }
                    }
                }
                Assert.True(sum2 > 0);

            }

            Benchmark.Dump();
        }

        [Test, Explicit("long running")]
        public void CompareSCGAndFastDictionaryWithSymbol()
        {
            var dictionary = new Dictionary<Symbol, int>();
            var fastDictionary = new FastDictionary<Symbol, int>();
            var symbols = new Symbol[1000];
            for (int i = 0; i < 1000; i++)
            {
                var s = new Symbol(i.ToString());
                symbols[i] = s;
                dictionary.Add(s, i);
                fastDictionary.Add(s, i);
            }

            const int count = 10000;

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                using (Benchmark.Run("Dictionary", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            sum += dictionary[symbols[j]];
                        }
                    }
                }

                Assert.True(sum > 0);

                var sum1 = 0L;
                using (Benchmark.Run("FastDictionary", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            sum1 += fastDictionary[symbols[j]];
                        }
                    }
                }
                Assert.True(sum > 0);

                Assert.AreEqual(sum, sum1);
            }
            Benchmark.Dump();
        }
    }
}