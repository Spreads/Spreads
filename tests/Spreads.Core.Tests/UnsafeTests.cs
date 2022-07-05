// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Shouldly;

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

    [Category("CI")]
    [TestFixture]
    public class UnsafeTests
    {
        [Test]
        public void CouldUseIDeltaMethods()
        {
            var first = new IntDelta { Value = 123 };
            var second = new IntDelta { Value = 456 };

            var delta = new IntDelta { Value = 456 - 123 };

            UnsafeEx.GetDeltaConstrained(ref first, ref second).ShouldBe(delta);
            UnsafeEx.AddDeltaConstrained(ref first, ref delta).ShouldBe(second);
        }

        [Test]
        public void CouldUseIDiffableMethods()
        {
            var first = new LongDiffable { Value = 123 };
            var second = new LongDiffable { Value = 456 };

            var diff = 456 - 123;

            UnsafeEx.DiffLongConstrained(ref first, ref second).ShouldBe(diff);
            UnsafeEx.AddLongConstrained(ref first, diff).ShouldBe(second);
        }

        [Test]
        public void CouldUseIDisposableMethods()
        {
            var disposable = new TestDisposable();
            UnsafeEx.DisposeConstrained(ref disposable);

            Assert.True(disposable.Disposed);
        }

        [Test]
        public void CouldUseGetHashCodeMethods()
        {
            var v = 1;
            var vh = UnsafeEx.GetHashCodeConstrained(ref v);

            vh.ShouldBe(v);

            var d = new TestDisposable();
            var dh = UnsafeEx.GetHashCodeConstrained(ref d);
            dh.ShouldBe(42);
        }

        [Test]
        public void CouldUseCompareAndEqualsMethods()
        {
            var v1 = new CompEq(1);
            var v2 = new CompEq(2);
            var v3 = new CompEq(2);

            UnsafeEx.CompareToConstrained(ref v1, ref v2).ShouldBe(-1);
            UnsafeEx.CompareToConstrained(ref v2, ref v1).ShouldBe(1);
            UnsafeEx.CompareToConstrained(ref v2, ref v3).ShouldBe(0);
            UnsafeEx.CompareToConstrained(ref v3, ref v1).ShouldBe(1);

            Assert.True(UnsafeEx.EqualsConstrained(ref v2, ref v3));
            Assert.False(UnsafeEx.EqualsConstrained(ref v1, ref v3));
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

        [Test]
        public void CeqCgtCltWork()
        {
            // int32
            UnsafeEx.Ceq(1, 1).ShouldBe(1);
            UnsafeEx.Ceq(1, 2).ShouldBe(0);

            UnsafeEx.Cgt(2, 1).ShouldBe(1);
            UnsafeEx.Cgt(1, 1).ShouldBe(0);
            UnsafeEx.Cgt(0, 1).ShouldBe(0);

            UnsafeEx.Clt(1, 2).ShouldBe(1);
            UnsafeEx.Clt(1, 1).ShouldBe(0);
            UnsafeEx.Clt(1, 0).ShouldBe(0);

            // int64
            UnsafeEx.Ceq(1L, 1L).ShouldBe(1);
            UnsafeEx.Ceq(1L, 2L).ShouldBe(0);

            UnsafeEx.Cgt(2L, 1L).ShouldBe(1);
            UnsafeEx.Cgt(1L, 1L).ShouldBe(0);
            UnsafeEx.Cgt(0L, 1L).ShouldBe(0);

            UnsafeEx.Clt(1L, 2L).ShouldBe(1);
            UnsafeEx.Clt(1L, 1L).ShouldBe(0);
            UnsafeEx.Clt(1L, 0L).ShouldBe(0);

            // explicit int32 -> int64
            UnsafeEx.Ceq(1, 1L).ShouldBe(1);
            UnsafeEx.Ceq(1, 2L).ShouldBe(0);

            UnsafeEx.Cgt(2, 1L).ShouldBe(1);
            UnsafeEx.Cgt(1, 1L).ShouldBe(0);
            UnsafeEx.Cgt(0, 1L).ShouldBe(0);

            UnsafeEx.Clt(1, 2L).ShouldBe(1);
            UnsafeEx.Clt(1, 1L).ShouldBe(0);
            UnsafeEx.Clt(1, 0L).ShouldBe(0);

            // IntPtr
            UnsafeEx.Ceq((IntPtr) 1, (IntPtr) 1).ShouldBe(1);
            UnsafeEx.Ceq((IntPtr) 1, (IntPtr) 2).ShouldBe(0);

            UnsafeEx.Cgt((IntPtr) 2, (IntPtr) 1).ShouldBe(1);
            UnsafeEx.Cgt((IntPtr) 1, (IntPtr) 1).ShouldBe(0);
            UnsafeEx.Cgt((IntPtr) 0, (IntPtr) 1).ShouldBe(0);

            UnsafeEx.Clt((IntPtr) 1, (IntPtr) 2).ShouldBe(1);
            UnsafeEx.Clt((IntPtr) 1, (IntPtr) 1).ShouldBe(0);
            UnsafeEx.Clt((IntPtr) 1, (IntPtr) 0).ShouldBe(0);

            // Float32
            UnsafeEx.Ceq(1.23, 1.23).ShouldBe(1);
            UnsafeEx.Ceq(1.23, 1.24).ShouldBe(0);

            UnsafeEx.Cgt(1.24, 1.23).ShouldBe(1);
            UnsafeEx.Cgt(1.23, 1.23).ShouldBe(0);
            UnsafeEx.Cgt(1.22, 1.23).ShouldBe(0);

            UnsafeEx.Clt(1.24, 1.23).ShouldBe(0);
            UnsafeEx.Clt(1.23, 1.23).ShouldBe(0);
            UnsafeEx.Clt(1.22, 1.23).ShouldBe(1);

            // Float64
            UnsafeEx.Ceq(1.23f, 1.23f).ShouldBe(1);
            UnsafeEx.Ceq(1.23f, 1.24f).ShouldBe(0);

            UnsafeEx.Cgt(1.24f, 1.23f).ShouldBe(1);
            UnsafeEx.Cgt(1.23f, 1.23f).ShouldBe(0);
            UnsafeEx.Cgt(1.22f, 1.23f).ShouldBe(0);

            UnsafeEx.Clt(1.24f, 1.23f).ShouldBe(0);
            UnsafeEx.Clt(1.23f, 1.23f).ShouldBe(0);
            UnsafeEx.Clt(1.22f, 1.23f).ShouldBe(1);

            UnsafeEx.BoolAsInt(true).ShouldBe(1);
            UnsafeEx.BoolAsInt(false).ShouldBe(0);
        }


    }
}
