// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using System;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class RetainedMemoryTests
    {
        [Test]
        public void CouldUseRetainedmemory()
        {
            var bytes = new byte[100];
            var rm = BufferPool.Retain(123456, true); // new RetainedMemory<byte>(bytes, default);
            // var ptr = rm.Pointer;
            var mem = rm.Memory;
            var clone = rm.Clone();
            var clone1 = rm.Clone();
            var clone2 = clone.Clone();
            rm.Dispose();
            clone.Dispose();
            clone1.Dispose();
            clone2.Dispose();
            var b = mem.Span[0];
        }

        [Test]
        public void CouldCreateRetainedmemoryFromArray()
        {
            var array = new byte[100];
            var rm = new RetainedMemory<byte>(array);

            var rmc = rm.Clone();

            rm.Dispose();

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            rmc.Span[1] = 1;

            // Assert.AreEqual(1, array[1]);
        }
    }
}