// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Experimental;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spreads.Core.Tests.Collections
{
    [Category("CI")]
    [TestFixture]
    public unsafe class SeriesAppendTests
    {
        [Test]
        public void CouldAppendSeries()
        {
            var sa = new AppendSeries<int, int>();

            Assert.IsTrue(sa.TryAddLast(1, 1).Result);
            Assert.IsFalse(sa.TryAddLast(1, 1).Result);

            Assert.IsTrue(sa.TryAddLast(2, 2).Result);

            Assert.Throws<KeyNotFoundException>(() =>
            {
                var _ = sa[0];
            });

            Assert.AreEqual(1, sa[1]);
            Assert.AreEqual(2, sa[2]);

            Assert.AreEqual(2, sa.Count());

            for (int i = 3; i < 32000; i++)
            {
                Assert.IsTrue(sa.TryAddLast(i, i).Result);
            }

            // TODO remove when implemented
            Assert.Throws<NotImplementedException>(() =>
            {
                for (int i = 32000; i < 33000; i++)
                {
                    Assert.IsTrue(sa.TryAddLast(i, i).Result);
                }
            });

            sa.Dispose();
        }
    }
}
