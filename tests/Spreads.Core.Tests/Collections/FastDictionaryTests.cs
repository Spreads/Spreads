// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Generic;
using Spreads.DataTypes;
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
            var fd = new FastDictionary<int, int>();

            for (int i = 0; i < 1000; i++)
            {
                d.Add(i, i);
                fd.Add(i, i);
            }

            const int count = 100000;

            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        sum += d[j];
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
                        sum += fd[j];
                    }
                }
                sw.Stop();
                Console.WriteLine($"FastDictionary {sw.ElapsedMilliseconds}");
                Assert.True(sum > 0);
            }
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