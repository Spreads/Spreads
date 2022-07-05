// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Shouldly;
using Spreads.Threading;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class AtomicCounterTests
    {
        [Test]
        public void CouldDispose()
        {
            int counter = 0;
            AtomicCounter.GetIsDisposed(ref counter).ShouldBeFalse();
            AtomicCounter.Increment(ref counter).ShouldBe(1);
            AtomicCounter.Increment(ref counter).ShouldBe(2);

            Assert.Throws<InvalidOperationException>(() => { AtomicCounter.Dispose(ref counter); });

            AtomicCounter.Decrement(ref counter).ShouldBe(1);
            AtomicCounter.DecrementIfOne(ref counter).ShouldBe(0);

            AtomicCounter.GetIsDisposed(ref counter).ShouldBeFalse();

            AtomicCounter.Dispose(ref counter);

            AtomicCounter.GetIsDisposed(ref counter).ShouldBeTrue();
        }

        [Test]
        public void CouldTryDispose()
        {
            int counter = 0;
            AtomicCounter.GetIsDisposed(ref counter).ShouldBeFalse();
            AtomicCounter.Increment(ref counter).ShouldBe(1);
            AtomicCounter.Increment(ref counter).ShouldBe(2);

            AtomicCounter.TryDispose(ref counter).ShouldBe(2);

            AtomicCounter.Decrement(ref counter).ShouldBe(1);
            AtomicCounter.DecrementIfOne(ref counter).ShouldBe(0);

            AtomicCounter.GetIsDisposed(ref counter).ShouldBeFalse();

            AtomicCounter.TryDispose(ref counter).ShouldBe(0);

            AtomicCounter.GetIsDisposed(ref counter).ShouldBe(true);

            AtomicCounter.TryDispose(ref counter).ShouldBe(-1);

            counter = AtomicCounter.Disposed - 1;

            Assert.Throws<InvalidOperationException>(() => { AtomicCounter.TryDispose(ref counter); });

            counter = AtomicCounter.Disposed | (123 << 24);

            AtomicCounter.TryDispose(ref counter).ShouldBe(-1);

            counter.ShouldBe(AtomicCounter.Disposed | (123 << 24));

            counter = (123 << 24);

            AtomicCounter.TryDispose(ref counter).ShouldBe(0);

            (counter & ~AtomicCounter.Disposed).ShouldBe((123 << 24));
        }
    }
}
