// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class RetainedMemoryTests
    {
       
        [Test]
        public unsafe void CouldUseRetainedmemory()
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


    }
}