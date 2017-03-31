// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Experimantal;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public class KeyComparerTests
    {
        [Test, Ignore]
        public unsafe void ComparerInterfaceAndCachedConstrainedComparer()
        {
            var c = Comparer<long>.Default;
            IComparer<long> ic = Comparer<long>.Default;
            var cc = KeyComparer<long>.Default;
            //var manualcc = new ConstrainedKeyComparerLong();

            const long count = 500000000L;

            var sw = new Stopwatch();

            for (int r = 0; r < 10; r++)
            {
                var sum = 0L;
                sw.Restart();
                for (long i = 0; i < count; i++)
                {
                    sum += c.Compare(i, i - 1L);
                }
                sw.Stop();
                Console.WriteLine($"Default comparer: {sw.ElapsedMilliseconds}");

                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (long i = 0; i < count; i++)
                {
                    sum += ic.Compare(i, i - 1L);
                }
                sw.Stop();
                Console.WriteLine($"Interface comparer: {sw.ElapsedMilliseconds}");

                Assert.True(sum > 0);

                sum = 0L;
                sw.Restart();
                for (long i = 0; i < count; i++)
                {
                    sum += cc.Compare(i, i - 1L);
                }
                sw.Stop();
                Console.WriteLine($"Constrained comparer: {sw.ElapsedMilliseconds}");
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