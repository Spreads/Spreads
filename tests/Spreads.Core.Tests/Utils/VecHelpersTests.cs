// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Native;
using Spreads.Utils;
using System;
using System.Linq;
using Spreads.DataTypes;

namespace Spreads.Core.Tests.Utils
{
    [TestFixture]
    public class VecHelpersTests
    {
        [Test, Explicit("long running")]
        public void BinarySearchBenchBench()
        {
            var rounds = 10;
            var counts = new int[] { 1000, 10_000, 100_000, 1_000_000 };
            for (int r = 0; r < rounds; r++)
            {
                foreach (var count in counts)
                {
                    var vec = new Vec<Timestamp>(Enumerable.Range(0, count).Select(x => (Timestamp)x).ToArray());

                    var mult = 10_000_000 / count;

                    using (Benchmark.Run("Vec BS " + count, count * mult))
                    {
                        for (int m = 0; m < mult; m++)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                var idx = vec.DangerousBinarySearch(0, count, (Timestamp)i, KeyComparer<Timestamp>.Default);
                                if (idx < 0)
                                {
                                    ThrowHelper.FailFast(String.Empty);
                                }
                            }
                        }
                    }
                }
            }
            Benchmark.Dump();
        }
    }
}