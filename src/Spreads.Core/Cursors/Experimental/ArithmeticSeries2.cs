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
    public struct ArithmeticSeries2<TKey, TValue, TOp, TCursor> : 
        ISpecializedCursor<TKey, TValue, ArithmeticSeries2<TKey, TValue, TOp, TCursor>>, ISeries<TKey, TValue>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
        where TOp : struct, IOp<TValue>
    {
        internal readonly TValue _value;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        internal CursorState State { get; set; }

        internal ArithmeticSeries2(TCursor cursor, TValue value)
        {
            this = new ArithmeticSeries2<TKey, TValue, TOp, TCursor>();
            // NB factory could return a cursor in non-initialized state
            _value = value;
            _cursor = cursor;
            State = CursorState.None;
        }

        /// <inheritdoc />
        public KeyValuePair<TKey, TValue> Current
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> CurrentBatch
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IReadOnlySeries<TKey, TValue> Source => _cursor.Source;

        /// <inheritdoc />
        public TKey CurrentKey
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TValue CurrentValue
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return default(TOp).Apply(_cursor.CurrentValue, _value);
            }
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsReadOnly => _cursor.Source.IsReadOnly;

        /// <inheritdoc />
        //public Task<bool> Updated => _cursor.Source.Updated;

        /// <inheritdoc />
        public ArithmeticSeries2<TKey, TValue, TOp, TCursor> Clone()
        {
            var clone = new ArithmeticSeries2<TKey, TValue, TOp, TCursor>(_cursor.Clone(), _value)
            {
                State = State
            };
            return clone;
        }

        /// <inheritdoc />
        public ArithmeticSeries2<TKey, TValue, TOp, TCursor> Initialize()
        {
            var toUse = new ArithmeticSeries2<TKey, TValue, TOp, TCursor>(_cursor, _value);
            toUse._cursor = toUse._cursor.Initialize();
            toUse.State = CursorState.Initialized;
            return toUse;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_cursor.TryGetValue(key, out var v))
            {
                value = default(TOp).Apply(v, _value);
                return true;
            }
            value = default(TValue);
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
            // TODO
            switch (State)
            {
                case CursorState.None:
                    break;

                case CursorState.Initialized:
                    break;

                case CursorState.Moving:
                    break;

                case CursorState.BatchMoving:
                    break;

                
                default:
                    throw new ArgumentOutOfRangeException();
            }
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

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        ICursor<TKey, TValue> ICursor<TKey, TValue>.Clone()
        {
            return Clone();
        }

        public CursorSeries2<TKey, TValue, ArithmeticSeries2<TKey, TValue, TOp, TCursor>> AsSeries()
        {
            return new CursorSeries2<TKey, TValue, ArithmeticSeries2<TKey, TValue, TOp, TCursor>>(this);
        }



        #region ISeries members

        public IDisposable Subscribe(IObserver<KeyValuePair<TKey, TValue>> observer)
        {
            throw new NotImplementedException();
        }

        IAsyncEnumerator<KeyValuePair<TKey, TValue>> IAsyncEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _cursor.Initialize();
        }

        public ICursor<TKey, TValue> GetCursor()
        {
            return _cursor.Initialize();
        }

        public object SyncRoot { get; }

        public Task<bool> Updated => throw new NotSupportedException();

        #endregion ISeries members



    }
}