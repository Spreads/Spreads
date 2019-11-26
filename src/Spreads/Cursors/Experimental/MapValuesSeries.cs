// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads.Cursors.Experimental
{
    /// <summary>
    /// A series that applies a selector to each value of its input series. Specialized for input cursor.
    /// </summary>
    [Obsolete("Use CursorSeries")]
    internal class MapValuesSeries<TKey, TValue, TResult, TCursor> :
        AbstractCursorSeries<TKey, TResult, MapValuesSeries<TKey, TValue, TResult, TCursor>>,
        ISpecializedCursor<TKey, TResult, MapValuesSeries<TKey, TValue, TResult, TCursor>> //, ICanMapValues<TKey, TResult>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        internal Func<TValue, TResult> _selector;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        public MapValuesSeries()
        {
        }

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal MapValuesSeries(TCursor cursor, Func<TValue, TResult> selector)
        {
            _cursor = cursor;
            _selector = selector;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TResult> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TResult>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TResult> CurrentBatch
        {
            get
            {
                throw new NotImplementedException();
                //var batch = _cursor.CurrentSpan;
                //// TODO when batching is proper implemented (nested batches) reuse an instance for this
                //var mapped = new MapValuesSeries<TKey, TValue, TResult, TCursor>(batch, _selector);
                //return mapped;
            }
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TResult CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _selector(_cursor.CurrentValue); }
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public override bool IsCompleted => _cursor.Source.IsCompleted;

        /// <inheritdoc />
        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public MapValuesSeries<TKey, TValue, TResult, TCursor> Clone()
        {
            var instance = GetUninitializedInstance();
            if (ReferenceEquals(instance, this))
            {
                // was not in use
                return this;
            }
            instance._cursor = _cursor.Clone();
            instance._selector = _selector;
            instance.State = State;
            return instance;
        }

        /// <inheritdoc />
        public override MapValuesSeries<TKey, TValue, TResult, TCursor> Initialize()
        {
            var instance = GetUninitializedInstance();
            instance._cursor = _cursor.Initialize();
            instance._selector = _selector;
            instance.State = CursorState.Initialized;
            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public override TResult GetAt(int idx)
        {
            return _selector(_cursor.Source.TryGetAt(idx));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TResult value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = _selector(v);
                return true;
            }
            value = default(TResult);
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            var moved = _cursor.MoveAt(key, direction);
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            var moved = _cursor.MoveFirst();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveLast()
        {
            var moved = _cursor.MoveLast();
            // keep navigating state unchanged
            if (moved && State == CursorState.Initialized)
            {
                State = CursorState.Moving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if ((int)State < (int)CursorState.Moving) return MoveFirst();
            return _cursor.MoveNext();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            var moved = await _cursor.MoveNextBatch(cancellationToken);
            if (moved)
            {
                State = CursorState.BatchMoving;
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if ((int)State < (int)CursorState.Moving) return MoveLast();
            return _cursor.MovePrevious();
        }

        /// <inheritdoc />
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, TResult> ICursor<TKey, TResult>.Clone()
        {
            return Clone();
        }

        //Series<TKey, TResult1> ICanMapValues<TKey, TResult>.Map<TResult1>(Func<TResult, TResult1> selector, Func<Buffer<TResult>, Memory<TResult1>> batchSelector)
        //{
        //    return new MapValuesSeries<TKey, TValue, TResult1, TCursor>(_series, CoreUtils.CombineMaps(_selector, selector));
        //}
    }
}