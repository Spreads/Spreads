// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors.Experimental
{
    internal sealed class UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> :
        CursorSeries<TKey, TResult, UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor>>,
        ISpecializedCursor<TKey, TResult, UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor>> //, ICanMapValues<TKey, TValue>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOp : struct, IOp<TValue, TValue2, TResult>
    {
        internal TValue2 _value;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        public UnaryOpSeries()
        {
        }

        /// <summary>
        /// MapValuesSeries constructor.
        /// </summary>
        internal UnaryOpSeries(TCursor cursor, TValue2 value)
        {
            _cursor = cursor;
            _value = value;
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
                throw new NotImplementedException("TODO see ArithmeticSeries implementation");
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
            get
            {
                return default(TOp).Apply(_cursor.CurrentValue, _value);
            }
        }

        /// <inheritdoc />
        public override KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public override bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public override bool IsReadOnly => _cursor.Source.IsReadOnly;

        /// <inheritdoc />
        public override Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> Clone()
        {
            var instance = GetUninitializedInstance();
            if (ReferenceEquals(instance, this))
            {
                // was not in use
                return this;
            }
            instance._cursor = _cursor.Clone();
            instance._value = _value;
            instance.State = State;
            return instance;
        }

        /// <inheritdoc />
        public override UnaryOpSeries<TKey, TValue, TValue2, TResult, TOp, TCursor> Initialize()
        {
            var instance = GetUninitializedInstance();
            instance._cursor = instance._cursor.Initialize();
            instance._value = _value;
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
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
        }

        /// <inheritdoc />
        public override TResult GetAt(int idx)
        {
            return default(TOp).Apply(_cursor.Source.GetAt(idx), _value);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TResult value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = default(TOp).Apply(v, _value);
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

        ICursor<TKey, TResult> ICursor<TKey, TResult>.Clone()
        {
            return Clone();
        }
    }
}