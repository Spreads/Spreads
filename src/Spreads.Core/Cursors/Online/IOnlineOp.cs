// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spreads.Cursors.Internal;

namespace Spreads.Cursors.Online
{
    /// <summary>
    /// A state machine that maintains a state of an online algorithm over a span and reacts to addition
    /// of new values and removal of old value. A span is defined by a pair of left and right cursors (inclusive)
    /// over a series. The cursors could move independently, but the right cursor must be ahead of or at
    /// the same place as the left one.
    /// </summary>
    /// <example>
    /// ...[L__span__R]...
    /// </example>
    public interface IOnlineOp<TKey, TValue, TResult, TCursor> : IDisposable
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        int Count { get; }

        /// <summary>
        /// Get a result from properly positioned span cursors.
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
        /// Internally the <see cref="IOnlineOp{TKey,TValue,TResult,TCursor}"/> is always implemented as
        /// a structure and could be copied by value. However, it may contain reference-typed fields as its state
        /// and the initial state must be recreated with this method.
        /// </remarks>
        void Reset();

        /// <summary>
        /// Add the current key/value of the right cursor to <see cref="IOnlineOp{TKey,TValue,TResult,TCursor}"/> state
        /// after the cursor has moved next and its current position has entered the current span.
        /// </summary>
        /// <param name="newRight">The right cursor of the span.</param>
        void AddNewRight(KeyValuePair<TKey, TValue> newRight);

        void RemoveOldRight(KeyValuePair<TKey, TValue> oldRight);

        /// <summary>
        /// Add the current key/value of the left cursor to <see cref="ISpanOp{TKey,TValue,TResult,TCursor}"/> state
        /// after the cursor has moved previous and its current position has entered the current span.
        /// </summary>
        /// <param name="newLeft">The left cursor of the span.</param>
        void AddNewLeft(KeyValuePair<TKey, TValue> newLeft);

        void RemoveOldLeft(KeyValuePair<TKey, TValue> oldLeft);
    }

    internal struct NoopOnlineOp<TKey, TValue, TCursor> : IOnlineOp<TKey, TValue, TValue, TCursor>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        public bool IsForwardOnly => false;

        public int Count => -1;

        public int MinWidth => -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewRight(KeyValuePair<TKey, TValue> newRight)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldRight(KeyValuePair<TKey, TValue> oldRight)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNewLeft(KeyValuePair<TKey, TValue> newLeft)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveOldLeft(KeyValuePair<TKey, TValue> oldLeft)
        {
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetResult(ref TCursor left, ref TCursor right)
        {
            throw new NotSupportedException("NoOnlineOp is a stub to reuse LagStepImpl for lagged values without calculating any state");
        }
    }
}