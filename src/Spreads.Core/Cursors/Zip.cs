// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors
{
    // TODO (!) lazy value evaluation. Very important for recursive N sparse series because many values will be unused

    // TODO we now do not have reimplemented continuous series, do operators first

    public struct Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
        : ICursorSeries<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>
        where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
        where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
    {
        private enum Cont
        {
            Discrete = 0, // 00
            Right = 1,    // 01
            Left = 2,     // 10
            Both = 3      // 11
        }

        #region Cursor state

        // This region must contain all cursor state that is passed via constructor.
        // No additional state must be created.
        // All state elements should be assigned in Initialize and Clone methods
        // All inner cursors must be disposed in the Dispose method but references to them must be kept (they could be used as factories)
        // for re-initialization.

        // NB must be mutable, could be a struct
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursorLeft _leftCursor;

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        internal TCursorRight _rightCursor;

        // NB this is fixed for the lifetime of the cursor, branch prediction should work
        private Cont _cont; // TODO Clont/Init must set it

        private TKey _currentKey;


        // cont should also move, TGV should be called only when other cursor is properly positioned
        [Obsolete("Value must be lazy")] // for cont 
        private (TLeft left, TRight right) _currentValue;

        private KeyComparer<TKey> _cmp;

        private int _c;

        internal CursorState State { get; set; }

        #endregion Cursor state

        #region Constructors

        internal Zip(TCursorLeft leftCursor, TCursorRight rightCursor) : this()
        {
            if (!ReferenceEquals(leftCursor.Comparer, rightCursor.Comparer))
            {
                throw new ArgumentException("Comparers are not the same");
            }

            if (leftCursor.Source.IsIndexed || rightCursor.Source.IsIndexed)
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            _leftCursor = leftCursor;
            _rightCursor = rightCursor;
            _cmp = _leftCursor.Comparer;
            _cont = (Cont)((_leftCursor.IsContinuous ? 2 : 0) + (_rightCursor.IsContinuous ? 1 : 0));
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        public Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight> Clone()
        {
            var instance =
                new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
                {
                    _leftCursor = _leftCursor.Clone(),
                    _rightCursor = _rightCursor.Clone(),
                    _cmp = _cmp,
                    _c = _c,
                    _cont = _cont,
                    State = State
                };

            return instance;
        }

        /// <inheritdoc />
        public Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight> Initialize()
        {
            var instance =
                new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
                {
                    _leftCursor = _leftCursor.Initialize(),
                    _rightCursor = _rightCursor.Initialize(),
                    _cmp = _cmp,
                    _c = default(int),
                    _cont = _cont,
                    State = CursorState.Initialized
                };
            // used only in Moving state
            return instance;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticCursor
            _leftCursor.Dispose();
            _rightCursor.Dispose();
            State = CursorState.None;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _leftCursor.Reset();
            _rightCursor.Reset();
            State = CursorState.Initialized;
        }

        ICursor<TKey, (TLeft, TRight)> ICursor<TKey, (TLeft, TRight)>.Clone()
        {
            return Clone();
        }

        #endregion Lifetime management


        #region ICursor members

        /// <inheritdoc />
        public KeyValuePair<TKey, (TLeft, TRight)> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new KeyValuePair<TKey, (TLeft, TRight)>(_currentKey, _currentValue); }
        }

        /// <inheritdoc />
        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _currentKey; }
        }

        /// <inheritdoc />
        public (TLeft, TRight) CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _currentValue;
            }
        }

        /// <inheritdoc />
        public IReadOnlySeries<TKey, (TLeft, TRight)> CurrentBatch => throw new NotImplementedException();

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _leftCursor.Comparer;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous => _cont == Cont.Both;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out (TLeft, TRight) value)
        {
            if (_leftCursor.TryGetValue(key, out var vl) && _rightCursor.TryGetValue(key, out var vr))
            {
                value = (vl, vr);
                return true;
            }
            value = default((TLeft, TRight));
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
            var d = (int)direction;

            switch (_cont)
            {
                case Cont.Discrete:
                    {
                        // we must move forward until keys are equal
                        // start from MF
                        var moved = _leftCursor.MoveAt(key, direction) && _rightCursor.MoveAt(key, direction);

                        while (moved)
                        {
                            _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                            if (_c < 0)
                            {
                                // left is behind
                                if (d < 2)
                                {
                                    // moving backward, should move right back
                                    moved = _rightCursor.MovePrevious();
                                }
                                else if (d > 2)
                                {
                                    // moving forward, should move left to next
                                    moved = _leftCursor.MoveNext();
                                }
                                else
                                {
                                    break; // EQ, cannot move
                                }
                            }
                            else if (_c > 0)
                            {
                                // left is ahead
                                if (d < 2)
                                {
                                    // moving backward, should move left back
                                    moved = _leftCursor.MovePrevious();
                                }
                                else if (d > 2)
                                {
                                    // moving forward, should move right to next
                                    moved = _rightCursor.MoveNext();
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }

                        moved = _c == 0 && moved;

                        if (moved)
                        {
                            State = CursorState.Moving;
                            _currentKey = _leftCursor.CurrentKey;
                            _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        else if (State == CursorState.Moving)
                        {
                            // recover position before the move, it must exist
                            // input cursor will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        return moved;
                    }
                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    break;
            }

            ThrowHelper.ThrowNotImplementedException();
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MoveNext
        public bool MoveFirst()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }

            switch (_cont)
            {
                case Cont.Discrete:
                    {
                        // we must move until keys are equal
                        var moved = _leftCursor.MoveFirst() && _rightCursor.MoveFirst();
                        while (moved)
                        {
                            _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                            if (_c < 0)
                            {
                                moved = _leftCursor.MoveNext();
                            }
                            else if (_c > 0)
                            {
                                moved = _rightCursor.MoveNext();
                            }
                            else
                            {
                                break;
                            }
                        }

                        moved = _c == 0 && moved;

                        if (moved)
                        {
                            State = CursorState.Moving;
                            _currentKey = _leftCursor.CurrentKey;
                            _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        else if (State == CursorState.Moving)
                        {
                            // recover position before the move, it must exist
                            // input cursor will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        return moved;
                    }

                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    {
                        //// at least one should move and the other must have a value at that place
                        //// if both have values, take he smaller one
                        //var moved = _leftCursor.MoveFirst() || _rightCursor.MoveFirst();
                        //if (!moved) return false;
                        //var c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                    }
                    break;
            }

            ThrowHelper.ThrowNotImplementedException();
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.NoInlining)] // NB NoInlining is important to speed-up MovePrevious
        public bool MoveLast()
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"ICursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            switch (_cont)
            {
                case Cont.Discrete:
                    {
                        // we must move until keys are equal
                        var moved = _leftCursor.MoveLast() && _rightCursor.MoveLast();
                        while (moved)
                        {
                            _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                            if (_c > 0)
                            {
                                moved = _leftCursor.MovePrevious();
                            }
                            else if (_c < 0)
                            {
                                moved = _rightCursor.MovePrevious();
                            }
                            else
                            {
                                break;
                            }
                        }

                        moved = _c == 0 && moved;

                        if (moved)
                        {
                            State = CursorState.Moving;
                            _currentKey = _leftCursor.CurrentKey;
                            _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        else if (State == CursorState.Moving)
                        {
                            // recover position before the move, it must exist
                            // input cursor will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        return moved;
                    }

                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    break;
            }
            ThrowHelper.ThrowNotImplementedException();
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            switch (_cont)
            {
                case Cont.Discrete:
                    {
                        bool moved;
                        do
                        {
                            moved = _c <= 0 ? _leftCursor.MoveNext() : _rightCursor.MoveNext();
                            _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                        } while (_c != 0 && moved);

                        moved = _c == 0 && moved;
                        if (!moved
                                && (_cmp.Compare(_currentKey, _leftCursor.CurrentKey) != 0
                                    || _cmp.Compare(_currentKey, _rightCursor.CurrentKey) != 0))
                        {
                            // recover position before the move, it must exist
                            // input cursors will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        else
                        {
                            _currentKey = _leftCursor.CurrentKey;
                            _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        return moved;
                    }

                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    break;
            }

            ThrowHelper.ThrowNotImplementedException();
            return false;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> MoveNextBatch(CancellationToken cancellationToken)
        {
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException($"CursorSeries {GetType().Name} is not initialized as a cursor. Call the Initialize() method and *use* (as IDisposable) the returned value to access ICursor MoveXXX members.");
            }
            // TODO (low) review if we could implement this
            return TaskEx.FalseTask;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

            switch (_cont)
            {
                case Cont.Discrete:
                    {
                        bool moved;
                        do
                        {
                            moved = _c >= 0 ? _leftCursor.MovePrevious() : _rightCursor.MovePrevious();
                            _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                        } while (_c != 0 && moved);

                        moved = _c == 0 && moved;
                        if (!moved
                            && (_cmp.Compare(_currentKey, _leftCursor.CurrentKey) != 0
                                || _cmp.Compare(_currentKey, _rightCursor.CurrentKey) != 0))
                        {
                            // recover position before the move, it must exist
                            // input cursors will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        else
                        {
                            _currentKey = _leftCursor.CurrentKey;
                            _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        return moved;
                    }

                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    break;
            }

            ThrowHelper.ThrowNotImplementedException();
            return false;
        }

        /// <inheritdoc />
        IReadOnlySeries<TKey, (TLeft, TRight)> ICursor<TKey, (TLeft, TRight)>.Source =>
            new Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Source =>
            new Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>(this);

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        #endregion ICursor members

        #region ICursorSeries members

        /// <inheritdoc />
        public bool IsIndexed => false; // TODO

        /// <inheritdoc />
        public bool IsReadOnly
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // TODO when all discretes are RO then this cursor is RO as well
            get { return _leftCursor.Source.IsReadOnly && _rightCursor.Source.IsReadOnly; }
        }

        /// <inheritdoc />
        public Task<bool> Updated
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // TODO
            get { return Task.WhenAny(_leftCursor.Source.Updated, _rightCursor.Source.Updated).Unwrap(); }
        }

        #endregion ICursorSeries members



        internal Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Map<TResult>(Func<TKey, (TLeft, TRight), TResult> selector)
        {
            return new Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>
                (this, selector);
        }

    }
}