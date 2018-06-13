// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// Filter cursor.
    /// </summary>
    public struct Filter<TKey, TValue, TCursor> :
        ICursorSeries<TKey, TValue, Filter<TKey, TValue, TCursor>>
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB Why three predicates? Checking them for null will be no more costly
        // then touching _cursor.CurrentKey/CurrentValue when it is not actually needed.
        // We could turn key/value predicates into the full one, but that will always touch values,
        // while our goal is to be as lazy as possible.
        // Null checks are constant during the lifetime of this cursor, so branch prediction should do well.
        internal Func<TKey, TValue, bool> _fullPredicate;

        internal Func<TKey, bool> _keyPredicate;
        internal Func<TValue, bool> _valuePredicate;

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursor _cursor;

        public CursorState State { get; internal set; }

        #endregion Cursor state

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ApplyPredicates()
        {
            if (_fullPredicate != null)
            {
                return _fullPredicate(_cursor.CurrentKey, _cursor.CurrentValue);
            }
            if (_keyPredicate != null)
            {
                return _keyPredicate(_cursor.CurrentKey);
            }
            if (_valuePredicate != null)
            {
                return _valuePredicate(_cursor.CurrentValue);
            }
            return false;
        }

        #region Constructors

        internal Filter(TCursor cursor, Func<TKey, TValue, bool> fullPredicate) : this()
        {
            _fullPredicate = fullPredicate;
            _cursor = cursor;
        }

        internal Filter(TCursor cursor, Func<TKey, bool> keyPredicate) : this()
        {
            _keyPredicate = keyPredicate;
            _cursor = cursor;
        }

        internal Filter(TCursor cursor, Func<TValue, bool> valuePredicate) : this()
        {
            _valuePredicate = valuePredicate;
            _cursor = cursor;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Filter<TKey, TValue, TCursor> Clone()
        {
            var instance = new Filter<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Clone(),
                _fullPredicate = _fullPredicate,
                _keyPredicate = _keyPredicate,
                _valuePredicate = _valuePredicate,
                State = State
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Filter<TKey, TValue, TCursor> Initialize()
        {
            var instance = new Filter<TKey, TValue, TCursor>
            {
                _cursor = _cursor.Initialize(),
                _fullPredicate = _fullPredicate,
                _keyPredicate = _keyPredicate,
                _valuePredicate = _valuePredicate,
                State = CursorState.Initialized
            };
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticCursor
            _cursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _cursor.Reset();
            State = CursorState.Initialized;
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
            get { return new KeyValuePair<TKey, TValue>(CurrentKey, CurrentValue); }
        }

        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.CurrentKey; }
        }

        /// <inheritdoc />
        public TValue CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _cursor.CurrentValue;
            }
        }

        /// <inheritdoc />
        public ISeries<TKey, TValue> CurrentBatch => throw new NotSupportedException();

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cursor.IsContinuous;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_keyPredicate != null && _keyPredicate(key))
            {
                return _cursor.TryGetValue(key, out value);
            }

            if (_cursor.TryGetValue(key, out var v))
            {
                if (_fullPredicate != null && !_fullPredicate(key, v))
                {
                    value = default;
                    return false;
                }
                if (_valuePredicate != null && !_valuePredicate(v))
                {
                    value = default;
                    return false;
                }
                value = v;
                return true;
            }

            value = default;
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }

            var wasMoving = State == CursorState.Moving;

            var ck = wasMoving ? _cursor.CurrentKey : default(TKey);

            var moved = _cursor.MoveAt(key, direction);

            if (moved && ApplyPredicates())
            {
                // done with true
                State = CursorState.Moving;
            }
            else if (direction == Lookup.EQ)
            {
                // either not moved or filter returns false, for EQ we cannot move to any direction
                moved = false;
            }
            else
            {
                do
                {
                    if (direction == Lookup.GE || direction == Lookup.GT)
                    {
                        moved = _cursor.MoveNext();
                    }
                    else
                    {
                        moved = _cursor.MovePrevious();
                    }
                } while (moved && !ApplyPredicates());
            }

            if (!moved && wasMoving && !_cursor.MoveAt(ck, Lookup.EQ))
            {
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNextAsync
        public bool MoveFirst()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            var moved = _cursor.MoveFirst();
            while (moved && !ApplyPredicates())
            {
                moved = _cursor.MoveNext();
            }
            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (State == CursorState.Moving)
            {
                // if cursor was moving that it must have had at least one value
                ThrowHelper.ThrowOutOfOrderKeyException(_cursor.CurrentKey);
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            var moved = _cursor.MoveLast();
            while (moved && !ApplyPredicates())
            {
                moved = _cursor.MovePrevious();
            }
            if (moved)
            {
                State = CursorState.Moving;
            }
            else if (State == CursorState.Moving)
            {
                // if cursor was moving that it must have had at least one value
                ThrowHelper.ThrowOutOfOrderKeyException(_cursor.CurrentKey);
            }
            return moved;
        }

        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            var ck = _cursor.CurrentKey;

            var moved = _cursor.MoveNext();

            while (moved && !ApplyPredicates())
            {
                moved = _cursor.MoveNext();
            }

            // TODO must stay at the very end - a special state that will make spinning on MN very cheap
            // at least count MN steps and move back with MP, not MA

            if (!moved && !_cursor.MoveAt(ck, Lookup.EQ))
            {
                // cannot recover, should be a very rare edge case
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
            }

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            return Utils.TaskUtil.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

            var ck = _cursor.CurrentKey;

            var moved = _cursor.MovePrevious();

            while (moved && !ApplyPredicates())
            {
                moved = _cursor.MovePrevious();
            }

            if (!moved && !_cursor.MoveAt(ck, Lookup.EQ))
            {
                // cannot recover, should be a very rare edge case
                ThrowHelper.ThrowOutOfOrderKeyException(ck);
            }

            return moved;
        }

        /// <inheritdoc />
        ISeries<TKey, TValue> ICursor<TKey, TValue>.Source => new Series<TKey, TValue, Filter<TKey, TValue, TCursor>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, TValue, Filter<TKey, TValue, TCursor>> Source => new Series<TKey, TValue, Filter<TKey, TValue, TCursor>>(this);

        /// <inheritdoc />
        public ValueTask<bool> MoveNextAsync()
        {
            // TODO (review) why not just fakse? If someone uses this directly then it's ok to return fasle - but that would be a lie about underlying series state (MNA=false means compeleted)
            throw new NotSupportedException("Struct cursors are only sync, must use an async wrapper");
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => _cursor.Source.IsIndexed;

        /// <inheritdoc />
        public bool IsCompleted
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.IsCompleted; }
        }

        /// <inheritdoc />
        public ValueTask<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cursor.Source.Updated; }
        }

        #endregion ICursorSeries members

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }
    }
}