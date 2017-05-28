// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System.Collections.Generic;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class KeyComparerTests
    {
        [Test, Ignore]
        public void ComparerInterfaceAndCachedConstrainedComparer()
        {
            var c = Comparer<long>.Default;
            IComparer<long> ic = Comparer<long>.Default;
            var cc = KeyComparer<long>.Default;

            const int count = 100000000;

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;

                using (Benchmark.Run("Default", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum += c.Compare(i, i - 1);
                    }
                }

                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("Interface", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum += ic.Compare(i, i - 1);
                    }
                }
                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("KeyComparer", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum += cc.Compare(i, i - 1);
                    }
                }
                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("Unsafe", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        sum += Unsafe.CompareToConstrained(i, i - 1);
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}