// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;

namespace Spreads.Core.Tests
{
    public struct IntDelta : IDelta<IntDelta>
    {
        public int Value;

        public IntDelta AddDelta(IntDelta delta)
        {
            return new IntDelta { Value = this.Value + delta.Value };
        }

        public IntDelta GetDelta(IntDelta other)
        {
            return new IntDelta { Value = other.Value - this.Value };
        }
    }

    public struct LongDiffable : Spreads.IInt64Diffable<LongDiffable>
    {
        public long Value;

        public LongDiffable Add(long diff)
        {
            return new LongDiffable { Value = this.Value + diff };
        }

        public int CompareTo(LongDiffable other)
        {
            return Value.CompareTo(other.Value);
        }

        public long Diff(LongDiffable other)
        {
            return other.Value - this.Value;
        }

    }

    [TestFixture]
    public class UnsafeTests
    {
        [Test]
        public void CouldUseIDeltaMethods()
        {
            var first = new IntDelta { Value = 123 };
            var second = new IntDelta { Value = 456 };

            var delta = new IntDelta { Value = 456 - 123 };

            Assert.AreEqual(delta, Unsafe.GetDeltaConstrained(first, second));
            Assert.AreEqual(second, Unsafe.AddDeltaConstrained(first, delta));
        }

        [Test]
        public void CouldUseIDiffableMethods()
        {
            var first = new LongDiffable { Value = 123 };
            var second = new LongDiffable { Value = 456 };

            var diff = 456 - 123;

            Assert.AreEqual(diff, Unsafe.DiffLongConstrained(first, second));
            Assert.AreEqual(second, Unsafe.AddLongConstrained(first, diff));
        }
    }
}