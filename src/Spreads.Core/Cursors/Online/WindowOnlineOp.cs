using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Cursors.Online
{
    /// <summary>
    /// Create a window from the current span.
    /// </summary>
    public struct WindowOnlineOp<TKey, TValue, TCursor> : IOnlineOp<TKey, TValue, Range<TKey, TValue, TCursor>, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private uint _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Range<TKey, TValue, TCursor> GetResult(ref TCursor left, ref TCursor right)
        {
            return new Range<TKey, TValue, TCursor>(left.Clone(), left.CurrentKey, right.CurrentKey, true, true, true, (int)_count);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (int)_count; }
        }

        public bool IsForwardOnly => false;
        public int MinWidth => 1;

        public void Dispose()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(KeyValuePair<TKey, TValue> newRight)
        {
            _count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldRight(KeyValuePair<TKey, TValue> oldRight)
        {
            _count--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(KeyValuePair<TKey, TValue> newLeft)
        {
            _count++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldLeft(KeyValuePair<TKey, TValue> oldLeft)
        {
            _count--;
        }
    }
}