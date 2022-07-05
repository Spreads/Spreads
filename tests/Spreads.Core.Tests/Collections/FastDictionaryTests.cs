// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Collections.Generic;
using NUnit.Framework;
using Shouldly;
using Spreads.Collections.Generic;
using Spreads.DataTypes;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class FastDictionaryTests
    {
        [Test, Explicit("long running")]
        public void CompareSCGAndFastDictionaryWithInts()
        {
            var dictionary = new Dictionary<long, long>();
            var dictionarySlim = new DictionarySlim<long, long>();
            var fastDictionary = new FastDictionary<long, long>();
            var concurrentDictionary = new ConcurrentDictionary<long, long>();

            var length = 10000;

            for (int i = 0; i < length; i++)
            {
                dictionary.Add(i, i);
                ref var x = ref dictionarySlim.GetOrAddValueRef(i);
                x = i;
                fastDictionary.Add(i, i);
                concurrentDictionary[i] = i;
            }

            const int count = 5000;

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                Benchmark.Run("Dictionary", () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            dictionary.TryGetValue(j, out var value);
                            sum += value;
                        }
                    }
                }, count * length);

                sum.ShouldBePositive();

                var sum1 = 0L;
                Benchmark.Run("FastDictionary", () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            fastDictionary.TryGetValue(j, out var value);
                            sum1 += value;
                        }
                    }
                }, count * length);
                sum1.ShouldBePositive();
                // Assert.AreEqual(sum, sum1);

                var sum2 = 0L;
                Benchmark.Run("ConcurrentDictionary", () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            concurrentDictionary.TryGetValue(j, out var value);
                            sum2 += value;
                        }
                    }
                }, count * length);
                sum2.ShouldBePositive();

                var sum3 = 0L;
                Benchmark.Run("DictionarySlim", () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < length; j++)
                        {
                            sum3 += dictionarySlim.TryGetValueRef(j, out _);
                        }
                    }
                }, count * length);
                sum3.ShouldBePositive();

            }

            Benchmark.Dump();
        }
    }
}
