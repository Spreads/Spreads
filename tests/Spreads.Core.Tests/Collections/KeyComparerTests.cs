// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spreads.Collections;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class KeyComparerTests
    {
        [Test, Ignore]
        public unsafe void ComparerInterfaceAndCachedConstrainedComparer()
        {
            var c = Comparer<int>.Default;
            IComparer<int> ic = Comparer<int>.Default;
            var cc = KeyComparer<int>.Default;
            var cc2 = Spreads.Collections.Experimental.KeyComparer<int>.Default;
            //var manualcc = new ConstrainedKeyComparerLong();

            const long count = 100000000L;

            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    sum += c.Compare(i, i - 1);
                }
                sw.Stop();
                Console.WriteLine($"Default comparer: {sw.ElapsedMilliseconds}");

                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    sum += ic.Compare(i, i - 1);
                }
                sw.Stop();
                Console.WriteLine($"Interface comparer: {sw.ElapsedMilliseconds}");

                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    sum += cc.Compare(i, i - 1);
                }
                sw.Stop();
                Console.WriteLine($"Key comparer: {sw.ElapsedMilliseconds}");
                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    sum += cc2.Compare(i, i - 1);
                }
                sw.Stop();
                Console.WriteLine($"Key comparer 2: {sw.ElapsedMilliseconds}");
                Assert.True(sum > 0);

                //sum = 0L;
                //sw.Restart();
                //for (long i = 0; i < count; i++)
                //{
                //    sum += manualcc.Compare(i, i - 1L);
                //}
                //sw.Stop();
                //Console.WriteLine($"Manual constrained comparer: {sw.ElapsedMilliseconds}");
                //Assert.True(sum > 0);
            }
        }
    }
}