using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Native;

namespace Spreads.Collections.Internal
{
    internal static class RingVecUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int IndexToOffset(int index, int head, int len)
        {
            // len is not guaranteed to be power of 2, cannot use bit mask
            // but we could avoid branch: we do not need modulo
            // instead we only need to subtract len if we wrap over it
            var offset = head + index;
            var isWrapped = offset >= len;
            var mult = *(int*)&isWrapped;
            var result = offset - mult * len;
            Debug.Assert(result >= 0);
            Debug.Assert(result < len);
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
