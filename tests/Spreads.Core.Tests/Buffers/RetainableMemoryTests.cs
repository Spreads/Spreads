// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using ObjectLayoutInspector;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using Spreads.Native;

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
                
                Assert.IsFalse(memory.IsPoolable);
                Assert.IsFalse(memory.IsPooled);
                Assert.IsFalse(memory.IsRetained);
                Assert.IsFalse(memory.IsDisposed);
                Assert.AreEqual(null, memory.Pool);
                Assert.AreEqual(memory.GetVec().Length, memory.Length);

                Assert.AreEqual(1, memory.PoolIndex, "0, memory._poolIdx");

                Assert.GreaterOrEqual(memory.Length, len, "memory.Length, len");
                var pow2Len = BitUtils.IsPow2(memory.Length) ? memory.Length : (BitUtils.NextPow2(memory.Length) / 2);
                Assert.AreEqual(pow2Len, memory.LengthPow2, "BitUtil.FindNextPositivePowerOfTwo(len) / 2, memory.LengthPow2");

                var rm = memory.Retain(0, len);
                Assert.AreEqual(1, memory.ReferenceCount, "1, memory.ReferenceCount");
                Assert.IsTrue(memory.IsRetained);

                Assert.AreEqual(len, rm.Length, "len, rm.Length");

                var rm1 = memory.Retain(len / 2, len / 4);
                Assert.AreEqual(2, memory.ReferenceCount);
                Assert.AreEqual(len / 4, rm1.Length);

                rm.Dispose();
                Assert.AreEqual(1, memory.ReferenceCount);

                rm1.Dispose();
                Assert.IsTrue(memory.IsDisposed);
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
