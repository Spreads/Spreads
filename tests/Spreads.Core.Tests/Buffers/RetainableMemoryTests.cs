// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using ObjectLayoutInspector;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using Shouldly;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class RetainableMemoryTests
    {
        // TODO DS SM
        public static object[] NonPooledFactories = new object[]
        {
            new object[] { (Func<int,RetainableMemory<byte>>)((len) => ArrayMemory<byte>.Create(BitUtils.NextPow2(len)))},
            new object[] { (Func<int,RetainableMemory<byte>>)((len) => PrivateMemory<byte>.Create(BitUtils.NextPow2(len)))}
        };

        // ReSharper disable once ClassNeverInstantiated.Local
        private class DummyRetainableMemory : RetainableMemory<byte>
        {
            public override Span<byte> GetSpan()
            {
                throw new NotImplementedException();
            }

            public override Spreads.Collections.Vec<byte> GetVec()
            {
                throw new NotImplementedException();
            }

            internal override void Free(bool finalizing)
            {
                throw new NotImplementedException();
            }
        }

        [Test, Explicit("")]
        public void RetainableMemoryLayout()
        {
            TypeLayout.PrintLayout<DummyRetainableMemory>();
        }

        [Test, Explicit("")]
        public void ArrayMemoryLayout()
        {
            TypeLayout.PrintLayout<ArrayMemory<byte>>();
        }

        [Test, Explicit("")]
        public void OffHeapMemoryLayout()
        {
            TypeLayout.PrintLayout<PrivateMemory<byte>>();
        }


        [Test, TestCaseSource(nameof(NonPooledFactories))]
        public void CouldRetain(Func<int, RetainableMemory<byte>> factory)
        {
            var lens = new[]
            {
                15, 16, 17, 127, 128, 129, 1023, 1024, 1025, 4095, 4096, 4097, 8191, 8192, 8193, 31 * 1024,
                32 * 1024, 45 * 1024
            };

            foreach (var len in lens)
            {
                var memory = factory(len);

                memory.IsPoolable.ShouldBe(false);
                memory.IsPooled.ShouldBe(false);
                memory.IsRetained.ShouldBe(false);
                memory.IsDisposed.ShouldBe(false);
                memory.Pool.ShouldBe(null);
                memory.Length.ShouldBe(memory.GetVec().Length);

                ((int)memory.PoolIndex).ShouldBe(1, "0, memory._poolIdx");

                Assert.GreaterOrEqual(memory.Length, len, "memory.Length, len");
                var pow2Len = BitUtils.IsPow2(memory.Length) ? memory.Length : (BitUtils.NextPow2(memory.Length) / 2);
                memory.LengthPow2.ShouldBe(pow2Len, "BitUtil.FindNextPositivePowerOfTwo(len) / 2, memory.LengthPow2");

                var rm = memory.Retain(0, len);
                memory.ReferenceCount.ShouldBe(1, "1, memory.ReferenceCount");
                memory.IsRetained.ShouldBe(true);

                rm.Length.ShouldBe(len, "len, rm.Length");

                var rm1 = memory.Retain(len / 2, len / 4);
                memory.ReferenceCount.ShouldBe(2);
                rm1.Length.ShouldBe(len / 4);

                rm.Dispose();
                memory.ReferenceCount.ShouldBe(1);

                rm1.Dispose();
                memory.IsDisposed.ShouldBe(true);
            }
        }

        [Test, TestCaseSource(nameof(NonPooledFactories))]
        public void CannotDisposeRetained(Func<int, RetainableMemory<byte>> factory)
        {
            var memory = factory(32 * 1024);
            var rm = memory.Retain();
            Assert.Throws<InvalidOperationException>(() => { ((IDisposable)memory).Dispose(); });
            rm.Dispose();
        }

        [Test, TestCaseSource(nameof(NonPooledFactories))]
        public void CannotDoubleDispose(Func<int, RetainableMemory<byte>> factory)
        {
            var memory = factory(32 * 1024);
            ((IDisposable)memory).Dispose();
            Assert.Throws<ObjectDisposedException>(() => { ((IDisposable)memory).Dispose(); });
        }

        [Test, TestCaseSource(nameof(NonPooledFactories))]
        public void CannotRetainDisposed(Func<int, RetainableMemory<byte>> factory)
        {
            var memory = factory(32 * 1024);
            ((IDisposable)memory).Dispose();

            Assert.Throws<ObjectDisposedException>(() => { var _ = memory.Retain(); });
            Assert.Throws<ObjectDisposedException>(() => { var _ = new RetainedMemory<byte>(memory, 0, memory.Length, false); });
        }
    }
}
