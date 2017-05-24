// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors
{
    // TODO (!) lazy value evaluation. Very important for recursive N sparse series because many values will be unused

    // TODO we now do not have reimplemented continuous series, do operators first

    // TODO recover could fail if e.g. we cleared series, should throw OOO exception

    public struct Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>
        : ICursorSeries<TKey, (TLeft, TRight), Zip<TKey, TLeft, TRight, TCursorLeft, TCursorRight>>
        where TCursorLeft : ISpecializedCursor<TKey, TLeft, TCursorLeft>
        where TCursorRight : ISpecializedCursor<TKey, TRight, TCursorRight>
    {
        private enum Cont : byte
        {
            None = 0,     // 00
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
        [Obsolete]
        private Cont _cont; // TODO Clont/Init must set it

        private (bool left, bool right) _isContinuous;
        private TKey _currentKey;

        // cont should also move, TGV should be called only when other cursor is properly positioned
        private (TLeft left, TRight right) _currentValue;

        private KeyComparer<TKey> _cmp;

        private int _c;

        //[Obsolete("Try to move without knowing results of previous moves.")]
        private (bool left, bool right) _everMoved;

        /// <summary>
        /// For continuous cursors we often cannot know if value exists without calling TGV,
        /// but the goal is to make value as lazy as possible and defer evaluation
        /// until CurrentValue is called.
        /// false means that a cursor is positioned at existing value
        /// </summary>
        private (bool left, bool right) _valueSet;

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
            _isContinuous = (_leftCursor.IsContinuous, _rightCursor.IsContinuous);
            _cont = (Cont)((_isContinuous.left ? 2 : 0) + (_isContinuous.right ? 1 : 0));
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
                    _isContinuous = _isContinuous,
                    _cont = _cont,
                    _currentKey = _currentKey,
                    _currentValue = _currentValue,
                    _cmp = _cmp,
                    _c = _c,
                    _everMoved = _everMoved,
                    _valueSet = _valueSet,

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
                    _isContinuous = _isContinuous,
                    _cont = _cont,
                    _cmp = _cmp,
                    _c = default(int),

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
                var left = _valueSet.left ? _currentValue.left : _leftCursor.CurrentValue;
                var right = _valueSet.right ? _currentValue.right : _rightCursor.CurrentValue;
                return (left, right);
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
                case Cont.None:
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
                                    // moving backward, should move R back
                                    moved = _rightCursor.MovePrevious();
                                }
                                else if (d > 2)
                                {
                                    // moving forward, should move L to next
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
                                    // moving backward, should move L back
                                    moved = _leftCursor.MovePrevious();
                                }
                                else if (d > 2)
                                {
                                    // moving forward, should move R to next
                                    moved = _rightCursor.MoveNext();
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                // LT or GT
                                if (d == 1 || d == 4) throw new NotImplementedException();
                                break;
                            }
                        }

                        moved = _c == 0 && moved;

                        if (moved)
                        {
                            State = CursorState.Moving;
                            _currentKey = _leftCursor.CurrentKey;
                            Debug.Assert(_valueSet.Equals((false, false)));
                            // NB no need to set value now: _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
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
                case Cont.None:

                    #region Cont.None

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
                            _everMoved = (true, true);
                            State = CursorState.Moving;
                            _currentKey = _leftCursor.CurrentKey;
                            Debug.Assert(_valueSet.Equals((false, false)));
                            // NB no need to set value now: _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
                        }
                        else if (State == CursorState.Moving)
                        {
                            // recover position before the move, it must exist
                            // input cursor will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        return moved;
                    }

                #endregion Cont.None

                case Cont.Right:
                    break;

                case Cont.Left:
                    break;

                case Cont.Both:
                    {
                        // at least one should move and the other must have a value at that place
                        // if both have values, take he smaller one
                        _everMoved = (_leftCursor.MoveFirst(), _rightCursor.MoveFirst());
                        var (lm, rm) = _everMoved;

                        var moved = false;
                        var currentKey = default(TKey);

                        // TODO (docs) Contract: continuous series consist of [segments], not (segments), i.e.
                        // all segments start and end with some existing point, inclusive: [start_point, end_point]
                        // otherwise will have to iterate too much in some cases to find `(start_point...` for zip
                        // (infinity, infinity), (infinity, end], [start, infinity) should work

                        if (lm && rm)
                        {
                            _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                            if (_c == 0)
                            {
                                // cursors are at the same place, no need to evaluate before CurrentValue is called
                                _valueSet = (false, false);
                                currentKey = _leftCursor.CurrentKey;
                                moved = true;
                            }
                            else if (_c < 0)
                            {
                                // left is behind, try to take the right's value matching left
                                if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var rv))
                                {
                                    // we have evaluated the right cursor
                                    _currentValue.right = rv;
                                    _valueSet = (false, true);

                                    currentKey = _leftCursor.CurrentKey;
                                    moved = true;
                                }
                                else
                                {
                                    // there could be no values before right's first one due to the `continuous inclusion assumtion` (TODO (doc) name this assumtion properly)

                                    // move left at or ahead of the right
                                    while (_c < 0 && _leftCursor.MoveNext())
                                    {
                                        _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);
                                    }

                                    if (_leftCursor.TryGetValue(_rightCursor.CurrentKey, out var lv))
                                    {
                                        // we have evaluated the left cursor
                                        _currentValue.left = lv;
                                        _valueSet = (true, false);

                                        currentKey = _leftCursor.CurrentKey;
                                        moved = true;
                                    }
                                    else
                                    {
                                        // TODO need a method to move next from a frontier,
                                        // here frontier is right's key
                                        throw new NotImplementedException();
                                    }
                                }
                            }
                            else
                            {
                                // TODO mirrow the _c < 0 case
                                throw new NotImplementedException();
                            }
                        }
                        else if (lm)
                        {
                            // right is empty, it could only has values defined on (inf,inf) or no values at all
                            if (_rightCursor.TryGetValue(_leftCursor.CurrentKey, out var v))
                            {
                                // we have evaluated the right cursor
                                _currentValue.right = v;
                                _valueSet = (false, true);

                                currentKey = _leftCursor.CurrentKey;
                                moved = true;
                            }
                            else
                            {
                                // ReSharper disable once EmptyStatement
                                ; // no values, do nothing
                            }
                        }
                        else if (rm)
                        {
                            // left is empty, it could only has values defined on (inf,inf) or no values at all
                            if (_leftCursor.TryGetValue(_leftCursor.CurrentKey, out var v))
                            {
                                // we have evaluated the right cursor
                                _currentValue.left = v;
                                _valueSet = (true, false);

                                currentKey = _rightCursor.CurrentKey;
                                moved = true;
                            }
                            else
                            {
                                // ReSharper disable once EmptyStatement
                                ; // no values, do nothing
                            }
                        }
                        else
                        {
                            // ReSharper disable once EmptyStatement
                            ; // none moved, do nothing
                        }

                        // TODO moved and currentKey should be shared for all cont cases
                        if (moved)
                        {
                            State = CursorState.Moving;
                            _currentKey = currentKey;
                        }
                        else if (State == CursorState.Moving)
                        {
                            // recover position before the move, it must exist
                            // input cursor will throw OOO if needed
                            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
                        }
                        return moved;
                    }
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
                case Cont.None:
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
                            Debug.Assert(_valueSet.Equals((false, false)));
                            // NB no need to set value now: _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
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

            while (true)
            {
                // move lagging or both if they are at the same position

                if (_c <= 0 || !rm)
                {
                    lm = _leftCursor.MoveNext();
                }
                if (_c >= 0 || !lm)
                {
                    rm = _rightCursor.MoveNext();
                }

                if (!(lm | rm))
                {
                    // none moved, exit with false
                    break;
                }

                _c = _leftCursor.Comparer.Compare(_leftCursor.CurrentKey, _rightCursor.CurrentKey);

                if (_c == 0)
                {
                    // This is a common case for all _isContinuous combinations - both cursors are at the same position.
                    moved = true;
                    _valueSet = (false, false);
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
                    var lc = _cmp.Compare(_leftCursor.CurrentKey, exclusiveFrontier);  // for debug breakpoint, will be optimized away in release
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
                            _valueSet = (true, false);

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
                        _valueSet = (false, true);

                        currentKey = _leftCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                    continue;
                }

                if (_c > 0)
                {
                    var rc = _cmp.Compare(_rightCursor.CurrentKey, exclusiveFrontier);
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
                            _valueSet = (false, true);

                            currentKey = _leftCursor.CurrentKey;
                            moved = true;
                        }
                        // ... because R.CurrentKey is in [L.CurrentKey, inf) - if we don't get value we will never get it.
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
                        _valueSet = (true, false);

                        currentKey = _rightCursor.CurrentKey;
                        moved = true;
                        break;
                    }
                }

                //ThrowHelper.ThrowInvalidOperationException("should have breaked!");
            }

            return moved;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (State < CursorState.Moving) return MoveFirst();

            Debug.Assert(_everMoved.left || _everMoved.right, "At least one cursor should have moved in MF");

            if (_everMoved.left && _everMoved.right)
            {
                var currentKey = default(TKey);
                var moved = MoveNextFromFrontier(_currentKey, ref currentKey);
                if (!moved
                    && (_cmp.Compare(_currentKey, _leftCursor.CurrentKey) != 0
                        || _cmp.Compare(_currentKey, _rightCursor.CurrentKey) != 0))
                {
                    // recover position before the move, it must exist
                    // input cursors will throw OOO if needed
                    if (!MoveAt(_currentKey, Lookup.EQ))
                    {
                        ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                    }
                }
                else
                {
                    _currentKey = currentKey;
                }
                return moved;
            }

            return MoveNextSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextSlow()
        {
            //if (_everMoved.left)
            //{
            //    if (_rightCursor.MoveNext())
            //    {
            //        // TODO check where moved
            //        _everMoved.right = true;
            //        TKey currentKey = default(TKey);
            //        var moved = MoveNextContFromFrontier(_currentKey, ref currentKey);
            //        if (!moved
            //            && (_cmp.Compare(_currentKey, _leftCursor.CurrentKey) != 0
            //                || _cmp.Compare(_currentKey, _rightCursor.CurrentKey) != 0))
            //        {
            //            // recover position before the move, it must exist
            //            // input cursors will throw OOO if needed
            //            if (!MoveAt(_currentKey, Lookup.EQ)) ThrowHelper.ThrowInvalidOperationException("Cannot recover position.");
            //        }
            //        else
            //        {
            //            _currentKey = currentKey;
            //        }
            //        return moved;
            //    }
            //}
            throw new NotImplementedException("TODO Zip on empty series");
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
                case Cont.None:
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
                            Debug.Assert(_valueSet.Equals((false, false)));
                            // NB no need to set value now: _currentValue = (_leftCursor.CurrentValue, _rightCursor.CurrentValue);
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