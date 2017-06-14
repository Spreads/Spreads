// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;

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

    public struct LongDiffable : IInt64Diffable<LongDiffable>
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

    public struct TestDisposable : IDisposable
    {
        public bool Disposed;

        public void Dispose()
        {
            Disposed = true;
        }

        public override int GetHashCode()
        {
            return 42;
        }
    }

    public struct CompEq : IComparable<CompEq>, IEquatable<CompEq>
    {
        public int Value;

        public CompEq(int value)
        {
            Value = value;
        }

        public int CompareTo(CompEq other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(CompEq other)
        {
            return Value.Equals(other.Value);
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

            Assert.AreEqual(delta, Unsafe.GetDeltaConstrained(ref first, ref second));
            Assert.AreEqual(second, Unsafe.AddDeltaConstrained(ref first, ref delta));
        }

        [Test]
        public void CouldUseIDiffableMethods()
        {
            var first = new LongDiffable { Value = 123 };
            var second = new LongDiffable { Value = 456 };

            var diff = 456 - 123;

            Assert.AreEqual(diff, Unsafe.DiffLongConstrained(ref first, ref second));
            Assert.AreEqual(second, Unsafe.AddLongConstrained(ref first, diff));
        }

        [Test]
        public void CouldUseIDisposableMethods()
        {
            var disposable = new TestDisposable();
            Unsafe.DisposeConstrained(ref disposable);

            Assert.True(disposable.Disposed);
        }

        [Test]
        public void CouldUseGetHashCodeMethods()
        {
            var v = 1;
            var vh = Unsafe.GetHashCodeConstrained(ref v);

            Assert.AreEqual(v, vh);

            var d = new TestDisposable();
            var dh = Unsafe.GetHashCodeConstrained(ref d);
            Assert.AreEqual(42, dh);
        }

        [Test]
        public void CouldUseCompareAndEqualsMethods()
        {
            var v1 = new CompEq(1);
            var v2 = new CompEq(2);
            var v3 = new CompEq(2);

            Assert.AreEqual(-1, Unsafe.CompareToConstrained(ref v1, ref v2));
            Assert.AreEqual(1, Unsafe.CompareToConstrained(ref v2, ref v1));
            Assert.AreEqual(0, Unsafe.CompareToConstrained(ref v2, ref v3));
            Assert.AreEqual(1, Unsafe.CompareToConstrained(ref v3, ref v1));

            Assert.True(Unsafe.EqualsConstrained(ref v2, ref v3));
            Assert.False(Unsafe.EqualsConstrained(ref v1, ref v3));
        }

        public static void Dispose<T>(ref T disposable) where T : IDisposable
        {
            disposable.Dispose();
        }

        public static long Diff<T>(ref T first, ref T second) where T : IInt64Diffable<T>
        {
            return first.Diff(second);
        }

        public static T Add<T>(ref T first, long diff) where T : IInt64Diffable<T>
        {
            return first.Add(diff);
        }

        public static T GetDelta<T>(ref T first, ref T second) where T : IDelta<T>
        {
            return first.GetDelta(second);
        }

        public static T AddDelta<T>(ref T first, ref T second) where T : IDelta<T>
        {
            return first.AddDelta(second);
        }

        public static int Compare<T>(ref T first, ref T second) where T : IComparable<T>
        {
            return first.CompareTo(second);
        }

        public static bool Equalr<T>(ref T first, ref T second) where T : IEquatable<T>
        {
            return first.Equals(second);
        }
    }
}