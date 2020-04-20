﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Spreads
{
    /// <summary>
    /// A cursor that inner-joins two series. Each continuous series is evaluated at every key of another series.
    /// To get full outer join make each series continuous by applying a transformation such as <see cref="Fill{TKey,TValue,TCursor}"/>
    /// or <see cref="RepeatWithKey{TKey,TValue,TCursor}"/>.
    /// </summary>
    public struct Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
        : ICursor<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>
        where TCursorLeft : ICursor<TKey, TLeft, TCursorLeft>
        where TCursorRight : ICursor<TKey, TRight, TCursorRight>
    {
        // TODO remove this and leave only the _isContinuous tuple
        private enum Cont : byte
        {
            None = 0,     // 00

            // ReSharper disable UnusedMember.Local
            Right = 1,    // 01

            Left = 2,     // 10

            // ReSharper restore UnusedMember.Local
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
        private Cont _cont;

        private (bool left, bool right) _isContinuous; // TODO keep only one of them

        private TKey _currentKey;

        /// <summary>
        /// Cached current value. If <see cref="_isValueSet"/> is true then the corresponding current value is already set
        /// and <see cref="CurrentValue"/> property should use it, otherwise it should use an inner cursor's CurrentValue.
        /// </summary>
        private (TLeft left, TRight right) _currentValue;

        /// <summary>
        /// For continuous cursors we often cannot know if value exists without calling TGV,
        /// but the goal is to make value as lazy as possible and defer evaluation
        /// until CurrentValue is called.
        /// False means that a cursor is positioned at existing value.
        /// </summary>
        private (bool left, bool right) _isValueSet;

        /// <summary>
        /// Locally cached comparer to avoid calls to cursors' properties, which could be a virtual call with a null check.
        /// </summary>
        private KeyComparer<TKey> _cmp;

        /// <summary>
        /// Comparer result of cursor keys, valid only when _everMoved == (true, true)
        /// </summary>
        private int _c;

        /// <summary>
        /// True if a cursor has moved at least once and it is safe to call its CurrentKey/CurrentValue members.
        /// </summary>
        private (bool left, bool right) _everMoved;

        private CursorState _state;

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _state; }
        }

        #endregion Cursor state

        #region Constructors

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Zip(TCursorLeft leftCursor, TCursorRight rightCursor)
        {
            if (!leftCursor.Comparer.Equals(rightCursor.Comparer))
            {
                throw new ArgumentException("Comparers are not the same");
            }

            if (leftCursor.IsIndexed || rightCursor.IsIndexed)
            {
                // TODO Should probably create a separate Lookup cursor that looks up keys from L in R via TGV
                // If R is continuous we still just call TGV. If L is continuous, then TVG on Lookup cursor with call TGV on both
                // Materialized (existing) keys will always be from L only. We cannot do efficient unions of keys on
                // unsorted cursors and there will be no way to do so without at least a hash table of processed cursors.
                throw new NotSupportedException("TODO Zip of indexed series is not supported.");
            }

            _leftCursor = leftCursor;
            _rightCursor = rightCursor;
            _cmp = _leftCursor.Comparer;
            _isContinuous = (_leftCursor.IsContinuous, _rightCursor.IsContinuous);
            _cont = (Cont)((_isContinuous.left ? 2 : 0) + (_isContinuous.right ? 1 : 0));
            _state = CursorState.Initialized;
            _everMoved = default;
            _c = default;
            _completer = null;
            _isValueSet = default;
            _currentKey = default;
            _currentValue = default;
        }

        #endregion Constructors

        #region Lifetime management

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight> Clone()
        {
            var instance =
                new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
                {
                    _leftCursor = _leftCursor.Clone(),
                    _rightCursor = _rightCursor.Clone(),
                    _isContinuous = _isContinuous,
                    _cont = _cont,
                    _currentKey = _currentKey,
                    _currentValue = _currentValue,
                    _isValueSet = _isValueSet,
                    _cmp = _cmp,
                    _c = _c,
                    _everMoved = _everMoved,

                    _state = _state
                };

            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight> Initialize()
        {
            var instance =
                new Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
                {
                    _leftCursor = _leftCursor.Initialize(),
                    _rightCursor = _rightCursor.Initialize(),
                    _isContinuous = _isContinuous,
                    _cont = _cont,
                    _cmp = _cmp,
                    _c = 0,

                    _state = CursorState.Initialized
                };
            // used only in Moving state
            return instance;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            // NB keep cursor state for reuse
            // dispose is called on the result of Initialize(), the cursor from
            // constructor could be uninitialized but contain some state, e.g. _value for this ArithmeticCursor
            _leftCursor.Dispose();
            _rightCursor.Dispose();
            _c = 0;
            _currentKey = default(TKey);
            _currentValue = default((TLeft, TRight));
            _isValueSet = (false, false);
            _everMoved = (false, false);

            _state = CursorState.None;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _leftCursor.Reset();
            _rightCursor.Reset();
            _c = 0;
            _currentKey = default(TKey);
            _currentValue = default((TLeft, TRight));
            _isValueSet = (false, false);
            _everMoved = (false, false);

            _state = CursorState.Initialized;
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
            get { return new KeyValuePair<TKey, (TLeft, TRight)>(_currentKey, CurrentValue); } // NB! not _currentValue
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
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
                if (!_isValueSet.left)
                {
                    _currentValue.left = _leftCursor.CurrentValue;
                    _isValueSet.left = true;
                }
                var left = _currentValue.left;

                if (!_isValueSet.right)
                {
                    _currentValue.right = _rightCursor.CurrentValue;
                    _isValueSet.right = true;
                }

                var right = _currentValue.right;
                return (left, right);
            }
        }

        /// <inheritdoc />
        public KeyComparer<TKey> Comparer => _cmp;

        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _cont == Cont.Both; }
        }

        /// <inheritdoc />
        public bool IsIndexed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return false; // TODO
            }
        }

        /// <inheritdoc />
        public bool IsCompleted
        {
            // NB this property is repeatedly called from MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // TODO when all discretes are RO then this cursor is RO as well
            get { return _leftCursor.IsCompleted && _rightCursor.IsCompleted; }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out (TLeft, TRight) value)
        {
            if (_leftCursor.TryGetValue(key, out var vl) && _rightCursor.TryGetValue(key, out var vr))
            {
                value = (vl, vr);
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

            var moved = false;
            var currentKey = default(TKey);

            var lk = _everMoved.left ? (hasKey: true, key: _leftCursor.CurrentKey) : (hasKey: false, key: default(TKey));
            var rk = _everMoved.right ? (hasKey: true, key: _rightCursor.CurrentKey) : (hasKey: false, key: default(TKey));

            var lm = _leftCursor.MoveAt(key, direction);
            var rm = _rightCursor.MoveAt(key, direction);
            _everMoved = (_everMoved.left || lm, _everMoved.right || rm);

            if (lm && rm)
            {
                // MoveNextFromFrontier/MovePreviousFromFrontier do the job
                var skipRecover = false;

                _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);

                if (_c == 0)
                {
                    // Both cursors have moved and satisfy the direction, therefore if they are at the same
                    // place they both satisfy the direction.
                    moved = true;
                    currentKey = _leftCursor.CurrentKey;
                    _isValueSet = (false, false);
                }
                else if (_c < 0)
                {
                    // forward moves
                    if (direction == Lookup.GE || direction == Lookup.GT)
                    {
                        var lc = _cmp.Compare(_leftCursor.CurrentKey, key);
                        Debug.Assert(lc >= 0);
                        // try to get value at L's key only for GE case
                        if ((direction == Lookup.GE
                            ||
                            lc > 0 // in this case both GE and GT could be evaluated at L's key
                            )
                            && _isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                        {
                            // we have evaluated the right cursor
                            _currentValue.right = rv;
                            _isValueSet = (false, true);

                            currentKey = _leftCursor.CurrentKey;
                            moved = true;
                        }
                        else
                        {
                            // L.CurrentKey is invalid so use it as an exclusive frontier
                            skipRecover = true;
                            moved = MoveNextFromFrontier(_leftCursor.CurrentKey, ref currentKey);
                        }
                    }
                    // backward moves
                    else if (direction == Lookup.LE || direction == Lookup.LT)
                    {
                        var rc = _cmp.Compare(_rightCursor.CurrentKey, key);
                        Debug.Assert(rc <= 0);
                        if ((direction == Lookup.LE
                             ||
                             rc < 0
                            ) && _isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                        {
                            _currentValue.left = lv;
                            _isValueSet = (true, false);

                            currentKey = _rightCursor.CurrentKey;
                            moved = true;
                        }
                        else
                        {
                            skipRecover = true;
                            moved = MovePreviousFromFrontier(_rightCursor.CurrentKey, ref currentKey);
                        }
                    }
                }
                else if (_c > 0)
                {
                    // NB this code mirrows the case _c < 0, any changes must be done to both
                    // here we change L and R

                    // forward moves
                    if (direction == Lookup.GE || direction == Lookup.GT)
                    {
                        var rc = _cmp.Compare(_rightCursor.CurrentKey, key);
                        Debug.Assert(rc >= 0);
                        // try to get value at L's key only for GE case
                        if ((direction == Lookup.GE
                             ||
                             rc > 0 // in this case both GE and GT could be evaluated at L's key
                            )
                            && _isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                        {
                            // we have evaluated the right cursor
                            _currentValue.left = lv;
                            _isValueSet = (true, false);

                            currentKey = _rightCursor.CurrentKey;
                            moved = true;
                        }
                        else
                        {
                            // L.CurrentKey is invalid so use it as an exclusive frontier
                            skipRecover = true;
                            moved = MoveNextFromFrontier(_rightCursor.CurrentKey, ref currentKey);
                        }
                    }
                    // backward moves
                    else if (direction == Lookup.LE || direction == Lookup.LT)
                    {
                        var lc = _cmp.Compare(_leftCursor.CurrentKey, key);
                        Debug.Assert(lc <= 0);
                        if ((direction == Lookup.LE
                             ||
                             lc < 0
                            ) && _isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                        {
                            _currentValue.right = rv;
                            _isValueSet = (false, true);

                            currentKey = _leftCursor.CurrentKey;
                            moved = true;
                        }
                        else
                        {
                            skipRecover = true;
                            moved = MovePreviousFromFrontier(_leftCursor.CurrentKey, ref currentKey);
                        }
                    }
                }

                if (!moved && !skipRecover)
                {
                    // recover those cursor whose values are not cached
                    if (lk.hasKey && !_isValueSet.left && _cmp.Compare(_leftCursor.CurrentKey, lk.key) != 0)
                    {
                        if (!_leftCursor.MoveAt(lk.key, Lookup.EQ))
                        {
                            ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                        }
                    }
                    if (rk.hasKey && !_isValueSet.right && _cmp.Compare(_rightCursor.CurrentKey, rk.key) != 0)
                    {
                        if (!_rightCursor.MoveAt(rk.key, Lookup.EQ))
                        {
                            ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                        }
                    }
                }
            }
            else if (lm)
            {
                // L is at position that satisfies the direction. R's segment here has inf at one of its end
                if (_isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                {
                    // we have evaluated the right cursor
                    _currentValue.right = rv;
                    _isValueSet = (false, true);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
                else
                {
                    if (lk.hasKey && !_isValueSet.left && _cmp.Compare(_leftCursor.CurrentKey, lk.key) != 0)
                    {
                        if (!_leftCursor.MoveAt(lk.key, Lookup.EQ))
                        {
                            ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                        }
                    }
                }
            }
            else if (rm)
            {
                if (_isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                {
                    _currentValue.left = lv;
                    _isValueSet = (true, false);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
                else
                {
                    if (rk.hasKey && !_isValueSet.right && _cmp.Compare(_rightCursor.CurrentKey, rk.key) != 0)
                    {
                        if (!_rightCursor.MoveAt(rk.key, Lookup.EQ))
                        {
                            ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                        }
                    }
                }
            }

            if (moved)
            {
                _currentKey = currentKey;
                _state = CursorState.Moving;
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

            var moved = false;
            var currentKey = default(TKey);

            var lm = _leftCursor.MoveFirst();
            var rm = _rightCursor.MoveFirst();

            _everMoved = (lm, rm);

            if (lm && rm)
            {
                _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                if (_c == 0)
                {
                    // first keys are equal, return true for all _cont cases
                    moved = true;
                    currentKey = _leftCursor.CurrentKey;
                    _isValueSet = (false, false);
                }
                else if (_c < 0)
                {
                    if (_isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                    {
                        // we have evaluated the right cursor
                        _currentValue.right = rv;
                        _isValueSet = (false, true);

                        currentKey = _leftCursor.CurrentKey;
                        moved = true;
                    }
                    else
                    {
                        // L.CurrentKey is invalid so use it as an exclusive frontier
                        moved = MoveNextFromFrontier(_leftCursor.CurrentKey, ref currentKey);
                    }
                }
                else if (_c > 0)
                {
                    if (_isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                    {
                        _currentValue.left = lv;
                        _isValueSet = (true, false);

                        currentKey = _rightCursor.CurrentKey;
                        moved = true;
                    }
                    else
                    {
                        moved = MoveNextFromFrontier(_rightCursor.CurrentKey, ref currentKey);
                    }
                }
            }
            else if (lm)
            {
                // R is empty, it is only possible to get value from it if it has it defined for (inf, inf)
                if (_isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                {
                    // we have evaluated the right cursor
                    _currentValue.right = rv;
                    _isValueSet = (false, true);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
            }
            else if (rm)
            {
                if (_isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                {
                    _currentValue.left = lv;
                    _isValueSet = (true, false);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
            }

            if (moved)
            {
                _state = CursorState.Moving;
                _currentKey = currentKey;
            }
            else if (State == CursorState.Moving)
            {
                // The cursor was already in the moving state but now cannot move first,
                // which could only mean it has become empty and some of it's input
                // was cleared, which is OutOfOrderKeyException. Do not try to recover but just throw.
                ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
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

            // NB this mirrows MoveFirst, any changes must be in the both places

            var moved = false;
            var currentKey = default(TKey);

            var lm = _leftCursor.MoveLast();
            var rm = _rightCursor.MoveLast();

            _everMoved = (lm, rm);

            if (lm && rm)
            {
                _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                if (_c == 0)
                {
                    // last keys are equal, return true for all _cont cases
                    moved = true;
                    currentKey = _leftCursor.CurrentKey;
                    _isValueSet = (false, false);
                }
                else if (_c < 0)
                {
                    if (_isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                    {
                        _currentValue.left = lv;
                        _isValueSet = (true, false);

                        currentKey = _rightCursor.CurrentKey;
                        moved = true;
                    }
                    else
                    {
                        moved = MovePreviousFromFrontier(_rightCursor.CurrentKey, ref currentKey);
                    }
                }
                else if (_c > 0)
                {
                    if (_isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                    {
                        // we have evaluated the right cursor
                        _currentValue.right = rv;
                        _isValueSet = (false, true);

                        currentKey = _leftCursor.CurrentKey;
                        moved = true;
                    }
                    else
                    {
                        // L.CurrentKey is invalid so use it as an exclusive frontier
                        moved = MovePreviousFromFrontier(_leftCursor.CurrentKey, ref currentKey);
                    }
                }
            }
            else if (lm)
            {
                // R is empty, it is only possible to get value from it if it has it defined for (inf, inf)
                if (_isContinuous.right && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                {
                    // we have evaluated the right cursor
                    _currentValue.right = rv;
                    _isValueSet = (false, true);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
            }
            else if (rm)
            {
                if (_isContinuous.left && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                {
                    _currentValue.left = lv;
                    _isValueSet = (true, false);

                    currentKey = _leftCursor.CurrentKey;
                    moved = true;
                }
            }

            if (moved)
            {
                _state = CursorState.Moving;
                _currentKey = currentKey;
            }
            else if (State == CursorState.Moving)
            {
                // The cursor was already in the moving state but now cannot move first,
                // which could only mean it has become empty and some of it's input
                // was cleared, which is OutOfOrderKeyException. Do not try to recover but just throw.
                ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            Debug.Assert(_everMoved.left || _everMoved.right, "At least one cursor should have moved in MF");

            bool moved;
            var currentKey = default(TKey);

            if (_everMoved.left && _everMoved.right)
            {
                moved = MoveNextFromFrontier(_currentKey, ref currentKey);
            }
            else
            {
                moved = MoveNextSlow(ref currentKey);
            }

            if (moved)
            {
                _currentKey = currentKey;
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextSlow(ref TKey currentKey)
        {
            if (_everMoved.left)
            {
                Debug.Assert(_isContinuous.right);
                Debug.Assert(_cmp.Compare(_currentKey, _leftCursor.CurrentKey) == 0);
                if (_rightCursor.MoveNext())
                {
                    _everMoved.right = true;
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                    // R moved first time
                    // if _c < 0 then R is ahead and L will be moved, normal case
                    // if _c == 0 then they are both at the current key
                    // if _c > 0 then R is behind _currentKey and it will be moved again
                    // we could lose its first value, but:
                    // if R's next value is ahead of F and L then we will call R.TGV and e.g. Repeat() will corretly return R's first value
                    // if R's next value is behind F and L we will move it again and again
                    return MoveNextFromFrontier(_currentKey, ref currentKey);
                }

                var lm = _leftCursor.MoveNext();
                if (lm && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                {
                    // we have evaluated the right cursor
                    _currentValue.right = rv;
                    _isValueSet = (false, true);

                    currentKey = _leftCursor.CurrentKey;
                    return true;
                }

                // recover - move previous if moved next
                if (lm && !_leftCursor.MovePrevious())
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                }
                return false;
            }

            if (_everMoved.right)
            {
                Debug.Assert(_isContinuous.left);
                Debug.Assert(_cmp.Compare(_currentKey, _leftCursor.CurrentKey) == 0);
                if (_leftCursor.MoveNext())
                {
                    _everMoved.left = true;
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                    return MoveNextFromFrontier(_currentKey, ref currentKey);
                }

                var rm = _rightCursor.MoveNext();
                if (rm && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                {
                    _currentValue.left = lv;
                    _isValueSet = (true, false);

                    currentKey = _leftCursor.CurrentKey;
                    return true;
                }

                if (rm && !_rightCursor.MovePrevious())
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                }
                return false;
            }

            ThrowHelper.ThrowInvalidOperationException("Cannot be in Moving state if none of the cursors ever moved.");
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MoveNextFromFrontier(TKey exclusiveFrontier, ref TKey currentKey)
        {
            Debug.Assert(_everMoved.Equals((true, true)),
                "MoveNextFromFrontier works only when the both cursors have moved at least once and their CurrentValue is valid.");
            Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) <= 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) <= 0,
                "At least one of the cursors must be at or behind the frontier.");

            var moved = false;

            // initial values to true so that first moves ignore them
            var lm = true;
            var rm = true;
            var lk = _leftCursor.CurrentKey;
            var rk = _rightCursor.CurrentKey;
            while (true)
            {
                // move lagging or both if they are at the same position

                // NB important to calculate the conditions before moves
                var shouldMoveLeft = _c <= 0 || !rm;
                var shouldMoveRight = _c >= 0 || !lm;
                if (shouldMoveLeft)
                {
                    lm = _leftCursor.MoveNext();
                }
                if (shouldMoveRight)
                {
                    rm = _rightCursor.MoveNext();
                }

                if (!(lm | rm))
                {
                    // none moved, exit with false
                    break;
                }

                _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);

                if (_c == 0)
                {
                    // This is a common case for all _isContinuous combinations - both cursors are at the same position.
                    moved = true;
                    currentKey = _leftCursor.CurrentKey;
                    _isValueSet = (false, false);
                    break;
                }

                if (_cont == Cont.None)
                {
                    // Discrete cursors must match wit h_c == 0.
                    continue;
                }

                if (_c < 0)
                {
                    // If L moved and is still behind the frontier, continue: with _c < 0 we
                    // will move only the left cursor one more time. If L is discrete then this move will be needed
                    // anyways; if L is continuous then we could miss L's key in the interval (F,R.CurrentKey).
                    // ReSharper disable once UnusedVariable
                    var lc = _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier); // for debug breakpoint, will be optimized away in release
                    if (lm && _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) <= 0)
                    {
                        continue;
                    }

                    // Now L is ahead of F or not moved (!lm), but L is behind R.
                    // If L not moved then the only way to get its value is if it is continuous -
                    // in this case check if R needs to move.
                    if (!lm)
                    {
                        var rc = _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier);
                        if (!_isContinuous.left)
                        {
                            // no way to get a value from stopped L
                            break;
                        }
                        if (rc <= 0)
                        {
                            // If L not moved then the second condition `(_c >= 0 || !lm)` will move R,
                            continue;
                        }

                        // Here L has stopped, R > F and we should try L.TGV(R.CurrentKey) and then break regardless of the result...
                        if (_leftCursor.TryGetValue(_rightCursor.CurrentKey, out var vl))
                        {
                            _currentValue.left = vl;
                            _isValueSet = (true, false);

                            currentKey = _rightCursor.CurrentKey;
                            moved = true;
                        }
                        // ... because R.CurrentKey is in [L.CurrentKey, inf) - if we don't get value we will never get it.
                        break;
                    }

                    Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) > 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) > 0);

                    // Now both cursors are ahead of F and L < R, so we should try R.TGV(L.CurrentKey).
                    // If R is discrete, we should continue because we cannot get value from it here.
                    if (!_isContinuous.right)
                    {
                        continue;
                    }

                    if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var vr))
                    {
                        _currentValue.right = vr;
                        _isValueSet = (false, true);

                        currentKey = _leftCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                    continue;
                }

                if (_c > 0)
                {
                    // ReSharper disable once UnusedVariable
                    var rc = _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier); // for debug breakpoint, will be optimized away in release
                    if (rm && _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) <= 0)
                    {
                        continue;
                    }

                    if (!rm)
                    {
                        var lc = _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier);
                        if (!_isContinuous.right)
                        {
                            break;
                        }
                        if (lc <= 0)
                        {
                            continue;
                        }

                        if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var vr))
                        {
                            _currentValue.right = vr;
                            _isValueSet = (false, true);

                            currentKey = _leftCursor.CurrentKey;
                            moved = true;
                        }
                        break;
                    }

                    Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) > 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) > 0);

                    if (!_isContinuous.left)
                    {
                        continue;
                    }

                    if (_leftCursor.TryGetValue(_rightCursor.CurrentKey, out var v))
                    {
                        // we have evaluated L
                        _currentValue.left = v;
                        _isValueSet = (true, false);

                        currentKey = _rightCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                }
            }

            if (!moved && State == CursorState.Moving)
            {
                var recovered = false;
                // recover those cursor whose values are not cached
                if (!_isValueSet.left && _cmp.Compare(_leftCursor.CurrentKey, lk) != 0)
                {
                    if (!_leftCursor.MoveAt(lk, Lookup.EQ))
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                    }
                    recovered = true;
                }
                if (!_isValueSet.right && _cmp.Compare(_rightCursor.CurrentKey, rk) != 0)
                {
                    if (!_rightCursor.MoveAt(rk, Lookup.EQ))
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                    }
                    recovered = true;
                }
                if (recovered)
                {
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                }
            }
            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            if (State < CursorState.Moving) return MoveLast();

            Debug.Assert(_everMoved.left || _everMoved.right, "At least one cursor should have moved in ML");

            bool moved;
            var currentKey = default(TKey);

            if (_everMoved.left && _everMoved.right)
            {
                moved = MovePreviousFromFrontier(_currentKey, ref currentKey);
            }
            else
            {
                moved = MovePreviousSlow(ref currentKey);
            }

            if (moved)
            {
                _currentKey = currentKey;
            }
            return moved;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MovePreviousSlow(ref TKey currentKey)
        {
            if (_everMoved.left)
            {
                Debug.Assert(_isContinuous.right);
                Debug.Assert(_cmp.Compare(_currentKey, _leftCursor.CurrentKey) == 0);
                if (_rightCursor.MovePrevious())
                {
                    _everMoved.right = true;
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                    return MovePreviousFromFrontier(_currentKey, ref currentKey);
                }

                var lm = _leftCursor.MovePrevious();
                if (lm && _rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                {
                    // we have evaluated the right cursor
                    _currentValue.right = rv;
                    _isValueSet = (false, true);

                    currentKey = _leftCursor.CurrentKey;
                    return true;
                }

                // recover - move previous if moved next
                if (lm && !_leftCursor.MoveNext())
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                }
                return false;
            }

            if (_everMoved.right)
            {
                Debug.Assert(_isContinuous.left);
                Debug.Assert(_cmp.Compare(_currentKey, _leftCursor.CurrentKey) == 0);
                if (_leftCursor.MovePrevious())
                {
                    _everMoved.left = true;
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                    return MovePreviousFromFrontier(_currentKey, ref currentKey);
                }

                var rm = _rightCursor.MovePrevious();
                if (rm && _leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                {
                    _currentValue.left = lv;
                    _isValueSet = (true, false);

                    currentKey = _leftCursor.CurrentKey;
                    return true;
                }

                if (rm && !_rightCursor.MoveNext())
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                }
                return false;
            }

            ThrowHelper.ThrowInvalidOperationException("Cannot be in Moving state if none of the cursors ever moved.");
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MovePreviousFromFrontier(TKey exclusiveFrontier, ref TKey currentKey)
        {
            Debug.Assert(_everMoved.Equals((true, true)),
                "MovePreviousFromFrontier works only when the both cursors have moved at least once and their CurrentValue is valid.");
            Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) >= 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) >= 0,
                "At least one of the cursors must be at or ahead the frontier.");

            var moved = false;

            // initial values to true so that first moves ignore them
            var lm = true;
            var rm = true;
            var lk = _leftCursor.CurrentKey;
            var rk = _rightCursor.CurrentKey;
            while (true)
            {
                // move lagging or both if they are at the same position

                // NB important to calculate the conditions before moves
                var shouldMoveLeft = _c >= 0 || !rm;
                var shouldMoveRight = _c <= 0 || !lm;
                if (shouldMoveLeft)
                {
                    lm = _leftCursor.MovePrevious();
                }
                if (shouldMoveRight)
                {
                    rm = _rightCursor.MovePrevious();
                }

                if (!(lm | rm))
                {
                    // none moved, exit with false
                    break;
                }

                _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);

                if (_c == 0)
                {
                    // This is a common case for all _isContinuous combinations - both cursors are at the same position.
                    moved = true;
                    currentKey = _leftCursor.CurrentKey;
                    _isValueSet = (false, false);
                    break;
                }

                if (_cont == Cont.None)
                {
                    // Discrete cursors must match wit h_c == 0.
                    continue;
                }

                if (_c > 0)
                {
                    // ReSharper disable once UnusedVariable
                    var lc = _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier); // for debug breakpoint, will be optimized away in release
                    if (lm && _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) >= 0)
                    {
                        continue;
                    }

                    if (!lm)
                    {
                        var rc = _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier);
                        if (!_isContinuous.left)
                        {
                            break;
                        }
                        if (rc >= 0)
                        {
                            continue;
                        }

                        if (_leftCursor.TryGetValue(_rightCursor.CurrentKey, out var vl))
                        {
                            _currentValue.left = vl;
                            _isValueSet = (true, false);

                            currentKey = _rightCursor.CurrentKey;
                            moved = true;
                        }
                        break;
                    }

                    Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) < 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) < 0);

                    if (!_isContinuous.right)
                    {
                        continue;
                    }

                    if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var vr))
                    {
                        _currentValue.right = vr;
                        _isValueSet = (false, true);

                        currentKey = _leftCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                    continue;
                }

                if (_c < 0)
                {
                    // ReSharper disable once UnusedVariable
                    var rc = _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier); // for debug breakpoint, will be optimized away in release
                    if (rm && _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) >= 0)
                    {
                        continue;
                    }

                    if (!rm)
                    {
                        var lc = _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier);
                        if (!_isContinuous.right)
                        {
                            break;
                        }
                        if (lc >= 0)
                        {
                            continue;
                        }

                        if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var vr))
                        {
                            _currentValue.right = vr;
                            _isValueSet = (false, true);

                            currentKey = _leftCursor.CurrentKey;
                            moved = true;
                        }
                        break;
                    }

                    Debug.Assert(_cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier) < 0 || _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier) < 0);

                    if (!_isContinuous.left)
                    {
                        continue;
                    }

                    if (_leftCursor.TryGetValue(_rightCursor.CurrentKey, out var v))
                    {
                        // we have evaluated L
                        _currentValue.left = v;
                        _isValueSet = (true, false);

                        currentKey = _rightCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                }
            }

            if (!moved && State == CursorState.Moving)
            {
                var recovered = false;
                // recover those cursor whose values are not cached
                if (!_isValueSet.left && _cmp.Compare(_leftCursor.CurrentKey, lk) != 0)
                {
                    if (!_leftCursor.MoveAt(lk, Lookup.EQ))
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                    }
                    recovered = true;
                }
                if (!_isValueSet.right && _cmp.Compare(_rightCursor.CurrentKey, rk) != 0)
                {
                    if (!_rightCursor.MoveAt(rk, Lookup.EQ))
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                    }
                    recovered = true;
                }
                if (recovered)
                {
                    _c = _cmp.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                }
            }
            return moved;
        }

        /// <inheritdoc />
        ISeries<TKey, (TLeft, TRight)> ICursor<TKey, (TLeft, TRight)>.Source =>
            new Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>(this);

        /// <summary>
        /// Get a <see cref="Series{TKey,TValue,TCursor}"/> based on this cursor.
        /// </summary>
        public Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Source =>
            new Series<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>(this);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException("Should use AsyncCursor");
        }

        private IAsyncCompleter _completer;

        public IAsyncCompleter AsyncCompleter => IsCompleted
            ? null
            : _completer ?? (_completer = new ZipCompleter(_leftCursor, _rightCursor));

        private class ZipCompleter : IAsyncCompleter
        {
            private readonly IAsyncCompleter _leftCompleter;
            private readonly IAsyncCompleter _rightCompleter;

            public ZipCompleter(TCursorLeft left, TCursorRight right)
            {
                _leftCompleter = left.IsCompleted ? null : left.AsyncCompleter;
                _rightCompleter = right.IsCompleted ? null : right.AsyncCompleter;
            }

            private class ZipCompleterSubscription : IDisposable
            {
                private readonly IDisposable _left;
                private readonly IDisposable _right;

                public ZipCompleterSubscription(IDisposable left, IDisposable right)
                {
                    _left = left;
                    _right = right;
                }

                // ReSharper disable once UnusedParameter.Local
                private void Dispose(bool disposing)
                {
                    _left?.Dispose();
                    _right?.Dispose();
                }

                public void Dispose()
                {
                    GC.SuppressFinalize(this);
                    Dispose(true);
                }

                ~ZipCompleterSubscription()
                {
                    Dispose(false);
                }
            }

            public IDisposable Subscribe(IAsyncCompletable subscriber)
            {
                var left = _leftCompleter?.Subscribe(subscriber);
                var right = _rightCompleter?.Subscribe(subscriber);
                return new ZipCompleterSubscription(left, right);
            }
        }

        #endregion ICursor members

        internal Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Map<TResult>(Func<TKey, (TLeft, TRight), TResult> selector)
        {
            return new Map<TKey, (TLeft, TRight), TResult, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>
                (this, selector);
        }

        //internal Op2<TKey, TResult, TOp, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>> Apply<TOp, TResult>()
        //    where TOp : struct, IOp<TLeft, TRight, TResult>
        //{
        //    var op2 = new Op2<TKey, TResult, TOp, Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>(this);
        //    return op2;
        //}
        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask(Task.CompletedTask);
        }
    }
}