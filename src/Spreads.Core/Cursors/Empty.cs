// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// Empty cursor.
    /// </summary>
    public struct Empty<TKey, TValue> :
        ICursorSeries<TKey, TValue, Empty<TKey, TValue>>
    {
        #region Lifetime management

        /// <inheritdoc />
        public Empty<TKey, TValue> Clone()
        {
            return this;
        }

        /// <inheritdoc />
        public Empty<TKey, TValue> Initialize()
        {
            return this;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public void Reset()
        {
        }

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management

        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }

        /// <inheritdoc />
        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }

        /// <inheritdoc />
        public ISeries<TKey, TValue> CurrentBatch
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }

        public CursorState State => CursorState.Initialized;

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => KeyComparer<TKey>.Default;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => true;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNextAsync
        public bool MoveFirst()
        {
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            return false;
        }

        public long MoveNext(long stride, bool allowPartial)
        {
            return 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            return 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch()
        {
            return TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => new Series<TKey, TValue, Empty<TKey, TValue>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, Empty<TKey, TValue>> Source => new Series<TKey, TValue, Empty<TKey, TValue>>(this);

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            return new ValueTask<bool>(false);
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => false;

        /// <inheritdoc />
        public bool IsCompleted => true;

        /// <inheritdoc />
        public ValueTask Updated => new ValueTask(Task.FromException(new InvalidOperationException("IsCompleted must be checked before accessing Updated property.")));

        #endregion ICursorSeries members

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}