using Spreads.DataTypes;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spreads.Utils;

namespace Spreads.Cursors.Online
{

    /// <summary>
    /// IOnlineOp for variance, mean, sum and count.
    /// </summary>
    public struct Stat2OnlineOp<TKey, TValue, TCursor> : IOnlineOp<TKey, TValue, Stat2<TKey>, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private Stat2<TKey> _state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat2<TKey> GetResult(ref TCursor left, ref TCursor right)
        {
            var copy = _state;
            copy._start = left.CurrentKey;
            copy._end = right.CurrentKey;
            copy._endValue = DoubleUtil.GetDouble(right.CurrentValue);
            return copy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddValue(TValue newValue)
        {
            var x = DoubleUtil.GetDouble(newValue);
            _state.AddValue(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveValue(TValue oldValue)
        {
            var x = DoubleUtil.GetDouble(oldValue);
            _state.RemoveValue(x);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state._count; }
        }

        public bool IsForwardOnly => false;

        public int MinWidth => 2;

        public void Dispose()
        {
            _state = default(Stat2<TKey>);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _state = default(Stat2<TKey>);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(KeyValuePair<TKey, TValue> newRight)
        {
            AddValue(newRight.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldRight(KeyValuePair<TKey, TValue> oldRight)
        {
            RemoveValue(oldRight.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(KeyValuePair<TKey, TValue> newLeft)
        {
            AddValue(newLeft.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldLeft(KeyValuePair<TKey, TValue> oldLeft)
        {
            RemoveValue(oldLeft.Value);
        }
    }
}