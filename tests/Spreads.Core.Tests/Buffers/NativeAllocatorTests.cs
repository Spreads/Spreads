// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Buffers;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public unsafe class NativeAllocatorTests
    {
#if NET6_0
        private static NativeAllocator.AllocateDelegate DefaultAllocate()
        {
            return (nuint requiredSize, out nuint usableSize) =>
            {
                var ptr = (byte*)NativeMemory.Alloc(requiredSize);
                usableSize = requiredSize;
                return ptr;
            };
        }

        private static NativeAllocator.FreeDelegate DefaultFree()
        {
            return memory =>
            {
                NativeMemory.Free(memory);
            };
        }

        [Test]
        public void CouldSetAllocators()
        {
            NativeAllocator.SetDelegates(DefaultAllocate(), DefaultFree());
        }
#endif
    }
}
