using System;
using Spreads.Buffers;

namespace Spreads.Core.Tests.Collections
{
    public static class BuffersTestHelper
    {
        public static RetainableMemory<long> CreateFilledRM(int capacity)
        {
            var rm = PrivateMemory<long>.Create(capacity);
            var vec = rm.GetVec();
            for (long i = 0; i < rm.Length; i++)
            {
                vec.UnsafeSetUnaligned((IntPtr) i, i);
            }

            return rm;
        }
    }
}