// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using System;
using System.Threading;

namespace Spreads.Core.Tests.Buffers
{
    // [Category("CI")]
    [TestFixture]
    public class DirectBufferTests
    {
        [Test]
        public void CouldCompareDbs()
        {
            var rm0 = BufferPool.Retain(100, true);
            var rm1 = BufferPool.Retain(100, true);

            var db0 = rm0.ToDirectBuffer();
            var db1 = rm1.ToDirectBuffer();

            Assert.IsTrue(db0.Equals(db0));
            Assert.IsTrue(db0.Equals(db1));

            rm0.Dispose();
            rm1.Dispose();

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
        }

        [Test]
        public void CouldFillDbs()
        {
            var rm0 = BufferPool.Retain(100, true);
            var rm1 = BufferPool.Retain(100, true);

            var db0 = rm0.ToDirectBuffer();
            var db1 = rm1.ToDirectBuffer();

            Assert.IsTrue(db0.IsFilledWithValue(0));
            Assert.IsTrue(db1.IsFilledWithValue(0));

            Assert.IsTrue(db0.Equals(db0));
            Assert.IsTrue(db0.Equals(db1));

            db0.Fill(0, db1.Length, 1);

            Assert.IsFalse(db0.Equals(db1));
            Assert.IsTrue(db0.IsFilledWithValue(1));

            db1.Fill(0, db1.Length, 1);
            Assert.IsTrue(db0.Equals(db0));

            rm0.Dispose();
            rm1.Dispose();
        }
    }
}
