// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using System;
using System.Linq;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class WindowCursorTests
    {
        [Test]
        public void CouldUseWindowCursor()
        {
            SortedMap<int, double> sm = new SortedMap<int, double>();

            var count = 100;

            for (int i = 0; i < count; i++)
            {
                sm.Add(i, i);
            }

            var window = new Window<int, double, SortedMapCursor<int, double>>(sm.GetEnumerator(), 10, 1).Source;

            foreach (var pair in window)
            {
                var expected = ((2 * pair.Key - 10 + 1) / 2.0) * 10;
                Assert.AreEqual(expected, pair.Value.Values.Sum());
                Console.WriteLine(pair.Value.Values.Sum());
                //Console.WriteLine(keyValuePair.Value.Aggregate("", (st,kvp) => st + "," + kvp.Value));
            }

        }

    }
}