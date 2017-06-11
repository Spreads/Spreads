// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections
{
    // TODO Move to Collections.Tests project
    [TestFixture]
    public class SCMTests
    {
        [Test, Ignore]
        public void EnumerateScmSpeed()
        {
            const int count = 1000000;

            var sm = new SortedMap<int, int>();
            var scm = new SortedChunkedMap<int, int>();

            for (int i = 0; i < count; i++)
            {
                sm.Add(i, i);
                scm.Add(i, i);
            }

            var sum = 0L;

            for (int r = 0; r < 20; r++)
            {
                sum = 0L;
                using (Benchmark.Run("SM", count))
                {
                    foreach (var keyValuePair in sm)
                    {
                        sum += keyValuePair.Value;
                    }
                }
                Assert.True(sum > 0);

                sum = 0L;
                using (Benchmark.Run("SCM", count))
                {
                    foreach (var keyValuePair in scm)
                    {
                        sum += keyValuePair.Value;
                    }
                }
                Assert.True(sum > 0);
            }

            Benchmark.Dump();
        }
    }
}