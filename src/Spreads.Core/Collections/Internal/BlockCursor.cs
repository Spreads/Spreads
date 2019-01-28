// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Collections.Internal
{
    // TODO maybe it's possible to create a single cursor for all containers?

    // TODO problem: with synced moves we must not update cursor state unless succeeded
    // therefore we cannot use it as internal cursor of other cursors - CV access needs
    // to be synced. If we compare order version then we throw on CV getter, otherwise
    // we will throw on next move but CV could be stale.

    /// <summary>
    /// <see cref="Collections.Experimental.Series{TKey,TValue}"/> cursor implementation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)] // This struct will be aligned to IntPtr.Size bytes because it has references, but small fields could be packed within 8 bytes.
    internal struct BlockCursor<TKey> : ICursorNew<TKey>, ISpecializedCursor<TKey, DataBlock, BlockCursor<TKey>>
    {
        internal BaseContainer<TKey> _source;

        internal DataBlock _currentBlock;

        internal int _blockPosition;

        // TODO offtop, from empty to non-empty changes order from 0 to 1

        // TODO review/test order version overflow in AC

        /// <summary>
        /// Series order version saved at cursor creation to detect changes in series.
        /// Should only be checked for <see cref="Mutability.Mutable"/>, append-only does not change order.
        /// </summary>
        internal int _orderVersion;

        // Note: We need to cache CurrentKey:
        // * in most cases it is <= 8 bytes so the entire struct should be <= 32 bytes or 1/2 cache line;
        // * we use it to recover from OOO exceptions;
        // * it does not affect performance too much and evaluation will be needed anyways in most cases.
        internal TKey _currentKey;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor(BaseContainer<TKey> source)
        {
            _source = source;
            _blockPosition = -1;
            _currentBlock = source.DataSource == null ? source.DataBlock : DataBlock.Empty;
            _orderVersion = 0; // TODO
            _currentKey = default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException("This cursor is only used as a building block of other cursors.");
        }

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_source == null)
                {
                    return CursorState.None;
                }

                return _blockPosition >= 0 ? CursorState.Moving : CursorState.Initialized;
            }
        }

        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _source._ñomparer;
        }

        public bool MoveAt(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMoveNextBatch(out object batch)
        {
            batch = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public long Move(long stride, bool allowPartial)
        {
            long mc;
            ulong nextPosition;
            DataBlock nextBlock = null;
            TKey k;

        RETRY:

            var version = Volatile.Read(ref _source._version);
            {
                // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride < 0). // This case is rare.
                // Uninitialized multi-block case goes to rare as well as uninitialized MP
                nextPosition = unchecked((ulong)(_blockPosition + stride)); // long.Max + int.Max < ulong.Max
                if (nextPosition < (ulong)_currentBlock.RowLength)
                {
                    mc = stride;
                }
                else
                {
                    mc = MoveRare(stride, allowPartial, ref nextPosition, ref nextBlock);
                }

                // mc = MoveImpl(stride, allowPartial, out nextPosition, out nextBlock);
                k = _currentBlock.RowIndex.DangerousGetRef<TKey>((int)nextPosition); // Note: do not use _blockPosition, it's 20% slower than second cast to int
            }

            if (Volatile.Read(ref _source._nextVersion) != version)
            {
                // TODO review if this is logically correct to check order version only here? We do check is again in value getter later
                if (_orderVersion != _source._orderVersion.Count)
                {
                    ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
                }
                goto RETRY;
            }

            if (mc != 0)
            {
                _blockPosition = (int)nextPosition;
                _currentKey = k;
                if (nextBlock != null)
                {
                    _currentBlock = nextBlock;
                }
            }

            return mc;
        }

        /// <summary>
        /// Called when next position is outside current block. Must be pure and do not change state.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public long MoveRare(long stride, bool allowPartial, ref ulong nextPos, ref DataBlock nextBlock)
        {
            var localBlock = _currentBlock;

            // var nextPosition = unchecked((ulong)(_blockPosition + stride)); // long.Max + int.Max < ulong.Max
            // Debug.Assert(nextPosition >= (ulong)localBlock.RowLength);

            if (_source.DataSource == null)
            {
                if (_blockPosition < 0 && stride < 0)
                {
                    Debug.Assert(State == CursorState.Initialized);
                    var nextPosition = unchecked((localBlock.RowLength + stride));
                    if (nextPosition >= 0)
                    {
                        nextPos = (ulong)nextPosition;
                        return stride;
                    }

                    if (allowPartial)
                    {
                        nextPos = 0;
                        return -localBlock.RowLength;
                    }
                }

                if (allowPartial)
                {
                    // TODO test for edge cases
                    if (_blockPosition + stride >= localBlock.RowLength)
                    {
                        var mc = (localBlock.RowLength - 1) - _blockPosition;
                        nextPos = (ulong)(_blockPosition + mc);
                        return mc;
                    }
                    if (stride < 0) // cannot just use else without checks before, e.g. what if _blockPosition == -1 and stride == 0
                    {
                        {
                            var mc = _blockPosition;
                            nextPos = 0;
                            return -mc;
                        }
                    }
                }

                nextPos = 0;
                return 0;
            }
            else
            {
                return MoveSlow(stride, allowPartial, ref nextPos, ref nextBlock);
            }
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveSlow(long stride, bool allowPartial, ref ulong nextPos, ref DataBlock nextBlock)
        {
            // TODO another non-inlined slow method. We need do change block, so isolate as much as possible from the fast path
            // fetch next block, do total/remaining calcs
            ThrowHelper.ThrowNotImplementedException();
            nextPos = 0;
            nextBlock = default;
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            // TODO sync
            if (_source.DataSource == null)
            {
                if (_currentBlock.RowLength > 0)
                {
                    _blockPosition = 0;
                    return true;
                }

                return false;
            }
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            // TODO sync
            if (_source.DataSource == null)
            {
                if (_currentBlock.RowLength > 0)
                {
                    _blockPosition = _currentBlock.RowLength - 1;
                    return true;
                }

                return false;
            }
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // Impl via MV is not a deal breaker or not at all a difference, quick initial tests ~margin of error.
            // Need to spend time on proper MV implementation (that of cause favors MN is there is a choice).
            return Move(1, false) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return Move(1, false) != 0;
        }

        public TKey CurrentKey
        {
            // No need to sync this access
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentKey;
        }

        public DataBlock CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // TODO order version check?
                return _currentBlock;
            }
        }

        public Series<TKey, DataBlock, BlockCursor<TKey>> Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Series<TKey, DataBlock, BlockCursor<TKey>>(this.Initialize());
        }

        public IAsyncCompleter AsyncCompleter
        {
            get { throw new NotImplementedException(); }
        }

        ISeries<TKey, DataBlock> ICursor<TKey, DataBlock>.Source => Source;

        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor<TKey> Initialize()
        {
            var c = this;
            c.Reset();
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor<TKey> Clone()
        {
            var c = this;
            return c;
        }

        ICursor<TKey, DataBlock> ICursor<TKey, DataBlock>.Clone()
        {
            return Clone();
        }

        public bool TryGetValue(TKey key, out DataBlock value)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            _blockPosition = -1;
            _currentKey = default;
            _orderVersion = _source._orderVersion.Count;

            if (_source.DataSource != null)
            {
                _currentBlock = DataBlock.Empty;
            }
        }

        public KeyValuePair<TKey, DataBlock> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new KeyValuePair<TKey, DataBlock>(CurrentKey, CurrentValue);
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            Reset();
            _source = null;
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        #region Obsolete members

        public long MoveNext(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public long MovePrevious(long stride, bool allowPartial)
        {
            throw new NotImplementedException();
        }

        public bool IsIndexed
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsCompleted
        {
            get { throw new NotImplementedException(); }
        }

        #endregion Obsolete members
    }
}