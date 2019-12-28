using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Native;

namespace Spreads.Collections.Internal
{
    // TODO Create overloads in VectorSearch that work on ref start + head + len
    // We are hitting Vec getter in search here

    internal static class RingVecUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexToOffset(int index, int head, int len)
        {
            if (AdditionalCorrectnessChecks.Enabled && (head < 0 || len < 0))
            {
                BuffersThrowHelper.ThrowIndexOutOfRange();
            }
            // return (head + index) % len;
            // len is not guaranteed to be power of 2, cannot use bit mask
            // but we could avoid branch: we do not need modulo
            // instead we only need to subtract len if we wrap over it
            var offset = head + index;
            var isWrapped = (len - (offset + 1)) >> 31; // 0 or -1
            var result = offset + isWrapped * len;
            if (AdditionalCorrectnessChecks.Enabled)
            {
                ThrowHelper.Assert(result >= 0);
                ThrowHelper.Assert(result < len);
            }

            return result;
        }
    }

    internal struct RingVec<T> : IVector<T>
    {
        private readonly Vec _vec;
        private readonly int _head;
        private readonly int _count;

        public RingVec(Vec vec, int head, int count)
        {
            Debug.Assert(count <= vec.Length);
            Debug.Assert(head < vec.Length);
            _vec = vec;
            _head = head;
            _count = count;
        }

        public int Length => _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGetItem(int index)
        {
            return _vec.DangerousGetRef<T>(RingVecUtil.IndexToOffset(index, _head, _count));
        }

        public T GetItem(int index)
        {
            return _vec.GetRef<T>(RingVecUtil.IndexToOffset(index, _head, _count));
        }
    }
}
