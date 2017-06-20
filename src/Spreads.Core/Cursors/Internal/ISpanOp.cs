// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using Spreads.Cursors.Online;

namespace Spreads.Cursors.Internal
{
    // Responsibility for eagerness could be on cursor or on op.
    // If on op, then it must return at least one additional code: "I am OK but you could try expand more"
    // If a cursor gets such a response, it should try to expand but then it will have to undo last move.
    // This doesn't differ from the case when a cursor treats zero "I am OK" response from the first
    // added value as the same conditional OK. So for the SpanOpImpl cursor nothing changes from
    // adding additional responses from Expand(), and the cursor is responsible for moving as much as possible.
    //
    // internal enum ExpandAction // not needed, too complex but cursor logic remains the same
    // {
    //     Shrink = -2,
    //     TryShrink = -1,
    //     Ok = 0,
    //     TryExpand = 1,
    //     Expand = 2
    // }

    /// <summary>
    /// A state machine that maintains a state of an online algorithm over a span and reacts to addition
    /// of new values and removal of old value. A span is defined by a pair of left and right cursors (inclusive)
    /// over a series. The cursors could move independently, but the right cursor must be ahead of or at
    /// the same place as the left one.
    /// </summary>
    /// <example>
    /// ...[L__span__R]...
    /// </example>
    internal interface ISpanOp<TKey, TValue, TResult, TCursor> : IDisposable
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        int Count { get; }

        /// <summary>
        /// Get a result from properly positioned span cursors. <see cref="Expand"/> method must return
        /// zero when this method is called, otherwise the return value is not defined.
        /// </summary>
        TResult GetResult(ref TCursor left, ref TCursor right);

        /// <summary>
        /// True if only forward moves are supported. <see cref="MinWidth"/> must be positive if this property is true,
        /// otherwise <see cref="ICursor{TKey,TValue}.MoveAt"/>, <see cref="ICursor{TKey,TValue}.MovePrevious"/> and
        /// <see cref="ICursor{TKey,TValue}.MoveLast"/> methods cannot be implemented.
        /// </summary>
        bool IsForwardOnly { get; }

        /// <summary>
        /// Minimum number of elements that should be accumulated in a temporary storage for forward-only ops.
        /// </summary>
        int MinWidth { get; }

        /// <summary>
        /// Recreate state as if after construction.
        /// </summary>
        /// <remarks>
        /// Internally the <see cref="ISpanOp{TKey,TValue,TResult,TCursor}"/> is always implemented as
        /// a structure and could be copied by value. However, it may contain reference-typed fields as its state
        /// and the initial state must be recreated with this method.
        /// </remarks>
        void Reset();

        /// <summary>
        /// Returns a value indicating if the span needs to expand, to shrink or is in valid state.
        /// </summary>
        /// <remarks>
        /// The return value could depend on previous state. E.g. for a minimum-width span
        /// zero could be returned only after the previous value was positive (the span was too small)
        /// and after that the width of the span became more than the minimum; otherwise moving only the right
        /// cursor to the next position will alway satisfy the minimum width requirement and the span could
        /// grow indefinitely.
        /// </remarks>
        /// <param name="left">The left cursor of the span.</param>
        /// <param name="right">The right cursor of the span.</param>
        /// <returns>A positive value if the span needs to expand, a negative value if the span needs to shrink, zero if the span is valid.</returns>
        int Expand(ref TCursor left, ref TCursor right);

        /// <summary>
        /// Add the current key/value of the right cursor to <see cref="ISpanOp{TKey,TValue,TResult,TCursor}"/> state
        /// after the cursor has moved next and its current position has entered the current span.
        /// </summary>
        /// <param name="right">The right cursor of the span.</param>
        void AddNewRight(ref TCursor right);

        /// <summary>
        /// Try to MovePrevious the right cursor and remove its old key/value on successful move.
        /// </summary>
        /// <param name="right">The right cursor of the span.</param>
        /// <returns>True if the right cursor moved previous and its old value is removed.</returns>
        bool RemoveAndMovePreviousRight(ref TCursor right);

        /// <summary>
        /// Add the current key/value of the left cursor to <see cref="ISpanOp{TKey,TValue,TResult,TCursor}"/> state
        /// after the cursor has moved previous and its current position has entered the current span.
        /// </summary>
        /// <param name="left">The left cursor of the span.</param>
        void AddNewLeft(ref TCursor left);

        /// <summary>
        /// Try to MoveNext the left cursor and remove its old key/value on successful move.
        /// </summary>
        /// <param name="left">The left cursor of the span.</param>
        /// <returns>True if the left cursor moved next and its old value is removed.</returns>
        bool RemoveAndMoveNextLeft(ref TCursor left);
    }

    /// <summary>
    /// SpanOp with fixed number of elements.
    /// </summary>
    internal struct SpanOpCount<TKey, TValue, TResult, TCursor, TOnlineOp> : ISpanOp<TKey, TValue, TResult, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOnlineOp : struct, IOnlineOp<TKey, TValue, TResult, TCursor>
    {
        private readonly int _width;
        private readonly bool _allowIncomplete;

        private TOnlineOp _opState;

        public SpanOpCount(int width, bool allowIncomplete, TOnlineOp opState)
        {
            _width = width;
            _allowIncomplete = allowIncomplete;
            _opState = opState;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _opState.Count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetResult(ref TCursor left, ref TCursor right)
        {
            return _opState.GetResult(ref left, ref right);
        }

        // TODO test with true
        public bool IsForwardOnly => _opState.IsForwardOnly;

        public int MinWidth => _allowIncomplete ? _opState.MinWidth : Math.Max(_width, _opState.MinWidth);

        public void Dispose()
        {
            _opState.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _opState.Reset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Expand(ref TCursor left, ref TCursor right)
        {
            return _allowIncomplete && Count > 0 && Count <= _width
                ? 0
                : _width - Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(ref TCursor right)
        {
            _opState.AddNewRight(right.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMovePreviousRight(ref TCursor right)
        {
            var current = right.Current;
            var moved = right.MovePrevious();
            if (moved)
            {
                _opState.RemoveOldRight(current);
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(ref TCursor left)
        {
            _opState.AddNewLeft(left.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMoveNextLeft(ref TCursor left)
        {
            var current = left.Current;
            var moved = left.MoveNext();
            if (moved)
            {
                _opState.RemoveOldLeft(current);
            }
            return moved;
        }
    }

    internal struct SpanOpWidth<TKey, TValue, TResult, TCursor, TOnlineOp> : ISpanOp<TKey, TValue, TResult, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOnlineOp : struct, IOnlineOp<TKey, TValue, TResult, TCursor>
    {
        // NB
        // MinWidth = GE/GT (e.g. at least 5 min) - what Deedle implements, could take a value from previous close
        // MaxWidth = LE/LT (e.g. at most 5 min) - useful e.g. for liquidity indicator. For rare events it is
        //      preferable to exclude the next value outside the width.

        private readonly TKey _width;

        private readonly Lookup _lookup;

        private int _expand;

        private TOnlineOp _opState;

        public SpanOpWidth(TKey width, Lookup lookup, TOnlineOp opState)
        {
            if (lookup == Lookup.EQ)
            {
                // TODO (low) this is not very useful but could be done in principle.
                // See CouldCalculateSMAWithWidthEQ test with a failing condition. Now EQ could work only for some regular series.
                ThrowHelper.ThrowNotImplementedException("Lookup.EQ is not implemented for span width limit.");
            }
            _width = width;
            _lookup = lookup;
            _expand = int.MaxValue;

            _opState = opState;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _opState.Count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetResult(ref TCursor left, ref TCursor right)
        {
            return _opState.GetResult(ref left, ref right);
        }

        public bool IsForwardOnly => _opState.IsForwardOnly;

        public int MinWidth => _opState.MinWidth;

        public void Dispose()
        {
            _opState.Dispose();
            _expand = int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _opState.Reset();
            _expand = int.MaxValue;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (bool isInvalid, int expand) IsInvalid(ref TCursor lagged, ref TCursor current)
        {
            var diff = default(SubtractOp<TKey>).Apply(current.CurrentKey, lagged.CurrentKey);
            var cmp = current.Comparer.Compare(diff, _width);
            if (cmp > 0)
            {
                // Diff is too big, should shrink if invalid
                return (_lookup != Lookup.GE && _lookup != Lookup.GT, -1);
            }

            if (cmp < 0)
            {
                // Diff is too small, should expand if invalid
                return (_lookup != Lookup.LE && _lookup != Lookup.LT, 1);
            }

            // cmp == 0

            if (_lookup == Lookup.LT)
            {
                // need to shrink, equal is not valid
                return (true, -1);
            }

            if (_lookup == Lookup.GT)
            {
                // need to expand, equal is not valid
                return (true, 1);
            }

            return (false, 0);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Expand(ref TCursor left, ref TCursor right)
        {
            var (isInvalid, expand) = IsInvalid(ref left, ref right);
            int newExpandState;
            if (isInvalid)
            {
                // Invalid is never acceptable
                newExpandState = expand;
            }
            else
            {
                //newExpandState = 0;
                // was invalid but now is valid,
                // use this value
                if (_expand != 0)
                {
                    newExpandState = 0;
                }
                else
                {
                    // was valid and is still valid, need to retry to ensure we are as close to the limit as possible
                    if ((int)_lookup > 2)
                    {
                        // was GE/GT and still, try to shrink
                        newExpandState = -1;
                    }
                    else if ((int)_lookup < 2)
                    {
                        // was LE/LT and still, try to expand
                        newExpandState = 1;
                    }
                    else
                    {
                        // for equal it is OK
                        newExpandState = 0;
                    }
                }
            }
            _expand = newExpandState;
            return _expand;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(ref TCursor right)
        {
            _opState.AddNewRight(right.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMovePreviousRight(ref TCursor right)
        {
            var current = right.Current;
            var moved = right.MovePrevious();
            if (moved)
            {
                _opState.RemoveOldRight(current);
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(ref TCursor left)
        {
            _opState.AddNewLeft(left.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMoveNextLeft(ref TCursor left)
        {
            var current = left.Current;
            var moved = left.MoveNext();
            if (moved)
            {
                _opState.RemoveOldLeft(current);
            }
            return moved;
        }
    }

    internal struct SpanOp<TKey, TValue, TResult, TCursor, TOnlineOp> : ISpanOp<TKey, TValue, TResult, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOnlineOp : struct, IOnlineOp<TKey, TValue, TResult, TCursor>
    {
        private enum LimitType : byte
        {
            Count,
            Width,

            /// <summary>
            /// Expand while a predicate returns true.
            /// </summary>
            WhilePredicate,

            /// <summary>
            /// Expand while the sum of mapped value is below limit.
            /// </summary>
            MapSum
        }

        private readonly LimitType _limitType;

        //private Func<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>, bool> _whilePredicate;
        //private Func<KeyValuePair<TKey, TValue>, TResult> _sumMapper;
        //private TResult _sumLimit;

        private readonly bool _allowIncomplete;

        private readonly int _widthN;
        private readonly TKey _width;

        private readonly Lookup _lookup;

        private int _expand;

        private TOnlineOp _opState;

        public SpanOp(int width, bool allowIncomplete, TOnlineOp opState)
        {
            _limitType = LimitType.Count;

            _widthN = width;
            _allowIncomplete = allowIncomplete;

            _width = default(TKey);
            _lookup = Lookup.EQ;
            _expand = int.MaxValue;

            _opState = opState;
        }

        public SpanOp(TKey width, Lookup lookup, TOnlineOp opState)
        {
            _limitType = LimitType.Width;

            _widthN = 0;
            _allowIncomplete = false;

            _width = width;
            _lookup = lookup;
            _expand = int.MaxValue;

            _opState = opState;

            //_whilePredicate = null;
            //_sumMapper = null;
            //_sumLimit = default(TResult);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _opState.Count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResult GetResult(ref TCursor left, ref TCursor right)
        {
            return _opState.GetResult(ref left, ref right);
        }

        public bool IsForwardOnly => _opState.IsForwardOnly;

        public int MinWidth
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_limitType == LimitType.Count)
                {
                    return _allowIncomplete ? _opState.MinWidth : Math.Max(_widthN, _opState.MinWidth);
                }
                if (_limitType == LimitType.Width)
                {
                    return _opState.MinWidth;
                }

                ThrowHelper.ThrowNotImplementedException();
                return 0;
            }
        }

        public void Dispose()
        {
            _opState.Dispose();
            _expand = int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _opState.Reset();
            _expand = int.MaxValue;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (bool isInvalid, int expand) IsInvalidWidth(ref TCursor lagged, ref TCursor current)
        {
            var diff = default(SubtractOp<TKey>).Apply(current.CurrentKey, lagged.CurrentKey);
            var cmp = current.Comparer.Compare(diff, _width);
            if (cmp > 0)
            {
                // Diff is too big, should shrink if invalid
                return (_lookup != Lookup.GE && _lookup != Lookup.GT, -1);
            }

            if (cmp < 0)
            {
                // Diff is too small, should expand if invalid
                return (_lookup != Lookup.LE && _lookup != Lookup.LT, 1);
            }

            // cmp == 0

            if (_lookup == Lookup.LT)
            {
                // need to shrink, equal is not valid
                return (true, -1);
            }

            if (_lookup == Lookup.GT)
            {
                // need to expand, equal is not valid
                return (true, 1);
            }

            return (false, 0);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Expand(ref TCursor left, ref TCursor right)
        {
            if (_limitType == LimitType.Count)
            {
                return _allowIncomplete && Count > 0 && Count <= _widthN
                    ? 0
                    : _widthN - Count;
            }

            if (_limitType == LimitType.Width)
            {
                var (isInvalid, expand) = IsInvalidWidth(ref left, ref right);
                int newExpandState;
                if (isInvalid)
                {
                    // Invalid is never acceptable
                    newExpandState = expand;
                }
                else
                {
                    //newExpandState = 0;
                    // was invalid but now is valid,
                    // use this value
                    if (_expand != 0)
                    {
                        newExpandState = 0;
                    }
                    else
                    {
                        // was valid and is still valid, need to retry to ensure we are as close to the limit as possible
                        if ((int)_lookup > 2)
                        {
                            // was GE/GT and still, try to shrink
                            newExpandState = -1;
                        }
                        else if ((int)_lookup < 2)
                        {
                            // was LE/LT and still, try to expand
                            newExpandState = 1;
                        }
                        else
                        {
                            // for equal it is OK
                            newExpandState = 0;
                        }
                    }
                }
                _expand = newExpandState;
                return _expand;
            }

            ThrowHelper.ThrowNotImplementedException();
            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(ref TCursor right)
        {
            _opState.AddNewRight(right.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMovePreviousRight(ref TCursor right)
        {
            var current = right.Current;
            var moved = right.MovePrevious();
            if (moved)
            {
                _opState.RemoveOldRight(current);
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(ref TCursor left)
        {
            _opState.AddNewLeft(left.Current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMoveNextLeft(ref TCursor left)
        {
            var current = left.Current;
            var moved = left.MoveNext();
            if (moved)
            {
                _opState.RemoveOldLeft(current);
            }
            return moved;
        }
    }

    internal struct MAvgCount<TKey, TValue, TCursor> : ISpanOp<TKey, TValue, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private readonly int _width;
        private readonly bool _allowIncomplete;

        private TValue _sum;
        private uint _count;

        public MAvgCount(int width, bool allowIncomplete)
        {
            _width = width;
            _allowIncomplete = allowIncomplete;
            _sum = default(TValue);
            _count = 0;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (int)_count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult(ref TCursor left, ref TCursor right)
        {
            return GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult()
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

        // TODO test with true
        public bool IsForwardOnly => false;

        public int MinWidth => _allowIncomplete ? 1 : _width; // TODO 1 & _width to OnlineOp prop

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
        public int Expand(ref TCursor left, ref TCursor right)
        {
            return _allowIncomplete && _count > 0 && _count <= _width
                ? 0
                : _width - (int)_count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(ref TCursor right)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, right.CurrentValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMovePreviousRight(ref TCursor right)
        {
            var currentValue = right.CurrentValue;
            var moved = right.MovePrevious();
            if (moved)
            {
                _count--;
                _sum = default(SubtractOp<TValue>).Apply(_sum, currentValue);
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(ref TCursor left)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, left.CurrentValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMoveNextLeft(ref TCursor left)
        {
            var currentValue = left.CurrentValue;
            var moved = left.MoveNext();
            if (moved)
            {
                _count--;
                _sum = default(SubtractOp<TValue>).Apply(_sum, currentValue);
            }
            return moved;
        }
    }

    internal struct MAvgWidth<TKey, TValue, TCursor> : ISpanOp<TKey, TValue, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        // NB
        // MinWidth = GE/GT (e.g. at least 5 min) - what Deedle implements, could take a value from previous close
        // MaxWidth = LE/LT (e.g. at most 5 min) - useful e.g. for liquidity indicator. For rare events it is
        //      preferrable to exclude the next value outside the width.

        private readonly TKey _width;

        private readonly Lookup _lookup;

        private TValue _sum;
        private uint _count;
        private int _expand;

        public MAvgWidth(TKey width, Lookup lookup)
        {
            _width = width;
            _lookup = lookup;
            _sum = default(TValue);
            _count = 0;
            _expand = int.MaxValue;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (int)_count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult(ref TCursor left, ref TCursor right)
        {
            return GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult()
        {
            if (typeof(TValue) == typeof(double))
            {
                return (TValue)(object)((double)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(float))
            {
                return (TValue)(object)((float)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(int))
            {
                return (TValue)(object)((int)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(long))
            {
                return (TValue)(object)((long)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(uint))
            {
                return (TValue)(object)((uint)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(ulong))
            {
                return (TValue)(object)((ulong)(object)_sum / _count);
            }

            if (typeof(TValue) == typeof(decimal))
            {
                return (TValue)(object)((decimal)(object)_sum / _count);
            }

            return GetResultDynamic();
        }

        private TValue GetResultDynamic()
        {
            return (TValue)((dynamic)_sum / _count);
        }

        public bool IsForwardOnly => false;

        public int MinWidth => 0;

        public void Dispose()
        {
            _sum = default(TValue);
            _count = 0;
            _expand = int.MaxValue;
        }

        public void Reset()
        {
            _sum = default(TValue);
            _count = 0;
            _expand = int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (bool isInvalid, int expand) IsInvalid(ref TCursor lagged, ref TCursor current)
        {
            var diff = default(SubtractOp<TKey>).Apply(current.CurrentKey, lagged.CurrentKey);
            var cmp = current.Comparer.Compare(diff, _width);
            if (cmp > 0)
            {
                // Diff is too big, should shrink if invalid
                return (_lookup != Lookup.GE && _lookup != Lookup.GT, -1);
            }

            if (cmp < 0)
            {
                // Diff is too small, should expand if invalid
                return (_lookup != Lookup.LE && _lookup != Lookup.LT, 1);
            }

            // cmp == 0

            if (_lookup == Lookup.LT)
            {
                // need to shrink, equal is not valid
                return (true, -1);
            }

            if (_lookup == Lookup.GT)
            {
                // need to expand, equal is not valid
                return (true, 1);
            }

            return (false, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Expand(ref TCursor left, ref TCursor right)
        {
            var (isInvalid, expand) = IsInvalid(ref left, ref right);
            int newExpandState;
            if (isInvalid)
            {
                // Invalid is never acceptable
                newExpandState = expand;
            }
            else
            {
                //newExpandState = 0;
                // was invalid but now is valid,
                // use this value
                if (_expand != 0)
                {
                    newExpandState = 0;
                }
                else
                {
                    // was valid and is still valid, need to retry to ensure we are as close to the limit as possible
                    if ((int)_lookup > 2)
                    {
                        // was GE/GT and still, try to shrink
                        newExpandState = -1;
                    }
                    else if ((int)_lookup < 2)
                    {
                        // was LE/LT and still, try to expand
                        newExpandState = 1;
                    }
                    else
                    {
                        // for equal it is OK
                        newExpandState = 0;
                    }
                }
            }
            _expand = newExpandState;
            return _expand;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(ref TCursor right)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, right.CurrentValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMovePreviousRight(ref TCursor right)
        {
            var currentValue = right.CurrentValue;
            var moved = right.MovePrevious();
            if (moved)
            {
                _count--;
                _sum = default(SubtractOp<TValue>).Apply(_sum, currentValue);
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(ref TCursor left)
        {
            _count++;
            _sum = default(AddOp<TValue>).Apply(_sum, left.CurrentValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveAndMoveNextLeft(ref TCursor left)
        {
            var currentValue = left.CurrentValue;
            var moved = left.MoveNext();
            if (moved)
            {
                _count--;
                _sum = default(SubtractOp<TValue>).Apply(_sum, currentValue);
            }
            return moved;
        }
    }

    
}