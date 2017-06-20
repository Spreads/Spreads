using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Cursors.Online
{
    /// <summary>
    /// Online sum and average.
    /// </summary>
    public struct SumAvgOnlineOp<TKey, TValue, TCursor> : IOnlineOp<TKey, TValue, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private TValue _sum;
        private uint _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult(ref TCursor left, ref TCursor right)
        {
            return GetAverage();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetSum()
        {
            return _sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetAverage()
        {
            if (typeof(TValue) == typeof(double))
            {
                return (TValue)(object)((double)(object)_sum / (int)_count);
            }

            if (typeof(TValue) == typeof(float))
            {
                return (TValue)(object)((float)(object)_sum / (int)_count);
            }

            if (typeof(TValue) == typeof(int))
            {
                return (TValue)(object)((int)(object)_sum / (int)_count);
            }

            if (typeof(TValue) == typeof(long))
            {
                return (TValue)(object)((long)(object)_sum / (int)_count);
            }

            if (typeof(TValue) == typeof(uint))
            {
                return (TValue)(object)((uint)(object)_sum / (int)_count);
            }

            if (typeof(TValue) == typeof(ulong))
            {
                return (TValue)(object)((ulong)(object)_sum / (ulong)_count);
            }

            if (typeof(TValue) == typeof(decimal))
            {
                return (TValue)(object)((decimal)(object)_sum / (int)_count);
            }

            return GetAverageDynamic();
        }

        private TValue GetAverageDynamic()
        {
            return (TValue)((dynamic)_sum / (int)_count);
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
            _sum = default(TValue);
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _sum = default(TValue);
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(KeyValuePair<TKey, TValue> newRight)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, newRight.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldRight(KeyValuePair<TKey, TValue> oldRight)
        {
            _count--;
            _sum = default(SubtractOp<TValue>).Apply(_sum, oldRight.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(KeyValuePair<TKey, TValue> newLeft)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, newLeft.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldLeft(KeyValuePair<TKey, TValue> oldLeft)
        {
            _count--;
            _sum = default(SubtractOp<TValue>).Apply(_sum, oldLeft.Value);
        }
    }
}