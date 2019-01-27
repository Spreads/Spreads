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
    [StructLayout(LayoutKind.Sequential, Pack = 1)] // This struct will be aligned to IntPtr.Size bytes because it has references, but small fields could be packed within 8 bytes.
    internal struct BlockCursor<TKey> : ICursorNew<TKey>, ISpecializedCursor<TKey, DataBlock, BlockCursor<TKey>>
    {
        // We know TValue and container has fast generic methods, try to be less specific, maybe we could reuse SCursor for all containers
        internal BaseContainer<TKey> _source;

        /// <summary>
        /// Current DataBlock.
        /// </summary>
        internal DataBlock _currentBlock;

        internal int _blockPosition;

        // Should we store current? For immutables not, but then should have another place where to check flags
        // For mutables, we cannot avoid checking version in moves because returned values could be meaningful without current key/value
        // If we check during moves and not store current then current could be wrong

        // TODO from empty to non-empty changes order from 0 to 1

        ///// <summary>
        ///// Least two bytes of series order version to detect changes in series.
        ///// Should only be checked for <see cref="Mutability.Mutable"/>, append-only does not change order.
        ///// </summary>
        ///// <remarks>
        ///// Probability to miss order change is 1/65536 and very specific conditions must be met.
        ///// E.g. the cursor should not move while data is changed exactly 65536 times.
        ///// This is 15 micromorts (https://en.wikipedia.org/wiki/Micromort) and there is 2x more
        ///// chance to miss order change than to die during full marathon run (7 micromorts).
        ///// </remarks>

        internal readonly int _orderVersionToken;

        //internal int ReservedInt;
        //internal short ReservedShort;
        //internal byte ReservedByte;

        // We need to cache CurrentKey.
        // * in most cases it is <= 8 bytes so the entire struct should be <= 32 bytes or 1/2 cache line.
        // * we use to to recover from OOO exceptions.
        // * it does not affect performance too much
        internal TKey _currentKey;

        // TODO try make it with 32 bytes, above is 25
        // * we do not need to cache current if we check _orderVersionToken

        public BlockCursor(BaseContainer<TKey> source)
        {
            _source = source;
            _blockPosition = -1;
            _currentBlock = source.DataSource == null ? source.DataBlock : DataBlock.Empty;
            _orderVersionToken = 0; // TODO
            _currentKey = default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotImplementedException();
        }

        public CursorState State
        {
            get
            {
                if (_source == null)
                {
                    return CursorState.None;
                }

                if (_blockPosition >= 0)
                {
                    return CursorState.Moving;
                }

                return CursorState.Initialized;
            }
        }

        public KeyComparer<TKey> Comparer
        {
            get { throw new NotImplementedException(); }
        }

        public bool MoveAt(TKey key, Lookup direction)
        {
            throw new NotImplementedException();
        }

        bool ICursor<TKey, DataBlock>.MoveNext()
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMoveNextBatch(out object batch)
        {
            batch = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Move(long stride, bool allowPartial)
        {
            if (_source._flags.IsImmutable) // via source is even faster in initial tests and saves _flag field on this struct
            {
                return MoveNoSync(stride, allowPartial); // 520 MOPS, 600 MOPS w/o if
            }
            return MoveSync(stride, allowPartial); // 380 MOPS, 415 MOPS w/o if
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long MoveNoSync(long stride, bool allowPartial)
        {
            long mc;
            ulong nextPosition;
            DataBlock nextBlock;

            // TKey k; // for NoSync case we could set _currentKey at the end.
            // For synced one we must set it after order version check.

            // Synchronize this region. Read-lock code is standard but is slow/hard to implement as methods
            // We need to rewrite this method for synchronized case and apply synchronization over the next region
            {
                mc = MoveImpl(stride, allowPartial, out nextPosition, out nextBlock);
            }

            if (mc != 0)
            {
                _blockPosition = (int)nextPosition;
                _currentKey = _currentBlock.RowIndex.DangerousGetRef<TKey>((int)nextPosition); // Note: do not use _blockPosition, it's 20% slower than second cast to int
                if (nextBlock != null)
                {
                    _currentBlock = nextBlock;
                }
            }

            return mc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining // TODO (review) in initial tests this did not penalize the no-sync path
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveSync(long stride, bool allowPartial)
        {
            long mc;
            ulong nextPosition;
            DataBlock nextBlock;
            TKey k;

            long version;
        RETRY:
            version = Volatile.Read(ref _source._version);
            // Synchronize this region. Read-lock code is standard but is slow/hard to implement as methods
            // We need to rewrite this method for synchronized case and apply synchronization over the next region
            {
                mc = MoveImpl(stride, allowPartial, out nextPosition, out nextBlock);
                k = _currentBlock.RowIndex.DangerousGetRef<TKey>((int)nextPosition); // Note: do not use _blockPosition, it's 20% slower than second cast to int
            }

            if (Volatile.Read(ref _source._nextVersion) != version)
            {
                // TODO review if this is logically correct to check order version only here? We do check is again in value getter later
                if (_orderVersionToken != _source._orderVersion.Count)
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
        /// Move pure implementation to syncronize over.
        /// </summary>
        /// <param name="stride"></param>
        /// <param name="allowPartial"></param>
        /// <param name="nextPosition"></param>
        /// <param name="nextBlock"></param>
        /// <returns></returns>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveImpl(long stride, bool allowPartial, out ulong nextPosition, out DataBlock nextBlock)
        {
            // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride < 0). // This case is rare.
            long mc;
            // uninitialized multi-block case goes to rare as well as uninitialized MP
            nextPosition = unchecked((ulong)(_blockPosition + stride)); // long.Max + int.Max < ulong.Max
            if (nextPosition < (ulong)_currentBlock.RowLength)
            {
                nextBlock = null;
                mc = stride;
            }
            else
            {
                mc = MoveImplRare(stride, allowPartial, out nextPosition, out nextBlock);
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
        public long MoveImplRare(long stride, bool allowPartial, out ulong nextPos, out DataBlock nextBlock)
        {
            var localBlock = _currentBlock;

            // var nextPosition = unchecked((ulong)(_blockPosition + stride)); // long.Max + int.Max < ulong.Max
            // Debug.Assert(nextPosition >= (ulong)localBlock.RowLength);

            if (_source.DataSource == null)
            {
                nextBlock = null;
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
                // TODO another non-inlined slow method. We need do change block, so isolate as much as possible from the fast path
                // fetch next block, do total/remaining calcs
                ThrowHelper.ThrowNotImplementedException();
                nextPos = 0;
                nextBlock = default;
                return default;
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // Impl via MV is not a deal breaker or not at all a difference, quick initial tests ~margin of error.
            // Need to spend time on proper MV implementation (that of cause favors MN is there is a choice).
            return Move(1, false) != 0;
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

        #endregion Obsolete members

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return Move(1, false) != 0;
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

        public TKey CurrentKey
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentKey;
        }

        public DataBlock CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _currentBlock;
            }
        }

        public Series<TKey, DataBlock, BlockCursor<TKey>> Source
        {
            get { throw new NotImplementedException(); }
        }

        public IAsyncCompleter AsyncCompleter
        {
            get { throw new NotImplementedException(); }
        }

        ISeries<TKey, DataBlock> ICursor<TKey, DataBlock>.Source
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsContinuous
        {
            get { return false; }
        }

        public BlockCursor<TKey> Initialize()
        {
            throw new NotImplementedException();
        }

        public BlockCursor<TKey> Clone()
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

        ICursor<TKey, DataBlock> ICursor<TKey, DataBlock>.Clone()
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(TKey key, out DataBlock value)
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public KeyValuePair<TKey, DataBlock> Current
        {
            get { throw new NotImplementedException(); }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}