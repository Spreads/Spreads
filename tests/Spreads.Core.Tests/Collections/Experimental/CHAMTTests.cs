// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections.Experimental
{
    [TestFixture]
    public class CHAMTTests
    {
        [Test, Explicit("long running")]
        public void BasicTests()
        {
            const int count = 500_000;

            for (int r = 0; r < 10; r++)
            {
                var chamt = new FSharpx.Collections.Experimental.ChampHashMap<int, int>();

                using (Benchmark.Run("Add", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        chamt = chamt.Add(i, i);
                    }
                }

                using (Benchmark.Run("Get", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var val = chamt.TryGetValue(i);
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}
