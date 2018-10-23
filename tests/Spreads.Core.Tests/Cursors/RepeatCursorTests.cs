// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using System;
using System.Linq;

namespace Spreads.Core.Tests.Cursors
{
    [Category("CI")]
    [TestFixture]
    public class RepeatCursorTests
    {
        [Test]
        public void CouldUseRepeatWithKeyCursor()
        {
            SortedMap<int, double> sm1 = new SortedMap<int, double>();
            SortedMap<int, double> sm2 = new SortedMap<int, double>();

            var count = 10;

            for (int i = 1; i <= count; i++)
            {
                if (i % 2 != 0)
                {
                    sm1.Add(i, i);
                }
                else
                {
                    sm2.Add(i, i);
                }
            }

            // this fails
            var marketdata = sm1.RepeatWithKey().Zip(sm2.RepeatWithKey()).ToArray();
            // but this works
            //var marketdata2 = sm1.RepeatWithKey().Zip(sm2.RepeatWithKey(), (t1, t2) => (t1, t2)).ToArray();
            foreach (var x in marketdata)
            {
                Console.WriteLine($"{x.Key} - {x.Value.Item1.Item1} - {x.Value.Item1.Item2} - {x.Value.Item2.Item1} - {x.Value.Item2.Item2}");
            }
        }

        
    }
}