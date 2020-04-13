// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <see cref="Series{TKey,TValue}"/> cursor implementation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct BlockCursor<TKey, TValue, TContainer> : ICursor<TKey, DataBlock, BlockCursor<TKey, TValue, TContainer>>
        where TContainer : BaseContainer<TKey>
    {
        internal TContainer _source;

        /// <summary>
        /// Backing storage for <see cref="CurrentBlock"/>. Must never be used directly.
        /// </summary>
        [Obsolete("Use CurrentBlock")]
        private DataBlock _currentBlockStorage;

        internal DataBlock CurrentBlock
        {
#pragma warning disable 618
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentBlockStorage;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (!_currentBlockStorage.IsEmptySentinel)
                    _currentBlockStorage.Decrement();

                _currentBlockStorage = value;

                if (!value.IsEmptySentinel)
                    value.Increment();
            }
#pragma warning restore 618
        }

        internal int BlockIndex;

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

        internal TValue _currentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor(TContainer source)
        {
            _source = source;
            BlockIndex = -1;
#pragma warning disable 618
            _currentBlockStorage = DataBlock.Empty;
#pragma warning restore 618
            _orderVersion = 0;
            _currentKey = default!;
            _currentValue = default!;
            Debug.Assert(source.Data != null, "source.Data != null: must be DataBlock.Empty instead of null");
            if (source.IsDataBlock(out var db, out _))
                CurrentBlock = db;
        }

        public CursorState State
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_source == null)
                    return CursorState.None;

                return BlockIndex >= 0 ? CursorState.Moving : CursorState.Initialized;
            }
        }

        public KeyComparer<TKey> Comparer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _source._comparer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            if (State == CursorState.None)
            {
                ThrowCannotMove();
            }

            ThrowHelper.DebugAssert(!CurrentBlock.IsDisposed || CurrentBlock.IsEmptySentinel, "!CurrentBlock.IsDisposed || ReferenceEquals(CurrentBlock, DataBlock.Empty)");

            bool found;
            int nextPosition;
            DataBlock nextBlock;
            TValue v = default!;
            var sw = new SpinWait();

        RETRY:
            // TODO rework sync for order version
            var version = _source.Version;
            {
                found = _source.TryFindBlockAt(ref key, direction, out nextBlock, out nextPosition);
                if (found)
                {
                    v = GetCurrentValue(nextPosition);
                }
            }

            if (_source.NextOrderVersion != version)
            {
                // See Move comments
                EnsureSourceNotDisposed();
                EnsureOrder();
                sw.SpinOnce();
                goto RETRY;
            }

            if (found)
            {
                BlockIndex = nextPosition;
                _currentKey = key;
                _currentValue = v;
                if (nextBlock != null)
                {
                    CurrentBlock = nextBlock;
                }
            }

            return found;
        }

        [Obsolete("Use GetCurrentKeyValue")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TValue GetCurrentValue(int currentBlockIndex)
        {
            if (typeof(TContainer) == typeof(Series<TKey, TValue>))
            {
                return CurrentBlock.DangerousValue<TValue>(currentBlockIndex);
            }
            if (typeof(TContainer) == typeof(BaseContainer<TKey>))
            {
                return default;
            }
            ThrowHelper.ThrowNotImplementedException();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public long Move(long stride, bool allowPartial)
        {
            if (TryMove(stride, allowPartial, out var mc))
            {
                return mc;
            }
            ThrowCannotMove();
            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowCannotMove()
        {
            if (State == CursorState.None)
                ThrowHelper.ThrowInvalidOperationException("Cursor is not initialized");

            ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
        }

        // TODO docs on handling OOO
        /// <summary>
        /// Returns true if a move is valid or false if the source order changed
        /// since this cursor construction or last <see cref="MoveAt"/> move.
        /// </summary>
        /// <seealso cref="OutOfOrderKeyException{TKey}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool TryMove(long stride, bool allowPartial, out long moveCount)
        {
            if (State != CursorState.Moving)
            {
                if (State == CursorState.None)
                {
                    moveCount = 0;
                    return false;
                }
                _orderVersion = _source.OrderVersion;
            }

            ThrowHelper.DebugAssert(CurrentBlock != null && (!CurrentBlock.IsDisposed || CurrentBlock.IsEmptySentinel), "!CurrentBlock.IsDisposed || ReferenceEquals(CurrentBlock, DataBlock.Empty)");

            // TODO check perf if we always return current block, could avoid at least 2-3 null checks
            moveCount = Move(stride, allowPartial, out var newBlock, out var newBlockIndex);

            TKey k = default!;
            TValue v = default!;

            if (moveCount != 0)
            {
                ThrowHelper.DebugAssert(newBlock == null || newBlockIndex <= (ulong)newBlock.RowCount);
                // Note: do not use _blockPosition, it's 20% slower than second cast to int
                GetCurrentKeyValue(newBlock ?? CurrentBlock, (int)newBlockIndex, out k, out v);
            }

            if (_source.NextOrderVersion != _orderVersion)
            {
                // MN must compare to stored version, while MA
                // must have constant order version only during the move
                newBlock?.Decrement();
                moveCount = 0;
                return false;
            }

            if (moveCount != 0)
            {
                if (newBlock != null)
                    CurrentBlock = newBlock;

                BlockIndex = (int)newBlockIndex;
                _currentKey = k;
                _currentValue = v;
            }

            if (AdditionalCorrectnessChecks.Enabled)
            { if (moveCount != 0 
                  && ((moveCount != stride && !allowPartial) 
                      || allowPartial && 
                        (stride > 0 && moveCount > stride
                         ||
                         stride < 0 && moveCount < stride
                         )
                      )
                  ) { ThrowBadReturnValue(stride, moveCount, allowPartial); } }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetCurrentKeyValue(DataBlock dataBlock, int currentBlockIndex, out TKey key, out TValue value)
        {
            if (typeof(TContainer) == typeof(Series<TKey, TValue>))
            {
                dataBlock.DangerousGetRowKeyValue(currentBlockIndex, out key, out value);
                return;
            }
            if (typeof(TContainer) == typeof(BaseContainer<TKey>))
            {
                key = dataBlock.DangerousRowKey<TKey>(currentBlockIndex);
                value = default!;
                return;
            }
            ThrowHelper.ThrowNotImplementedException();
            key = default;
            value = default;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBadReturnValue(long stride, long mc, bool allowPartial)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"Return value mc={mc} does not correspond to stride=[{stride}] (allowPartial={allowPartial})");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long Move(long stride, bool allowPartial, out DataBlock? newBlock, out ulong newBlockIndex)
        {
            newBlock = null;

            long moveCount;

            // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride <= 0). This case is rare.
            // Uninitialized multi-block case goes to rare as well as uninitialized MP
            newBlockIndex = unchecked((ulong)(BlockIndex + stride)); // int.Max + long.Max < ulong.Max

            var rowCount = CurrentBlock.RowCount;

            if (AdditionalCorrectnessChecks.Enabled)
                ThrowHelper.Assert(rowCount >= 0, "rowCount >= 0 for all CurrentBlocks, empty sentinel has zero length specifically for this case");

            if (newBlockIndex < (ulong)rowCount)
            {
                moveCount = stride;
            }
            else
            {
                if (rowCount < CurrentBlock.RowCapacity & stride > 0)
                    return 0;

                moveCount = MoveRare(stride, allowPartial, out newBlock, out newBlockIndex);

                if (AdditionalCorrectnessChecks.Enabled && moveCount != 0 && newBlockIndex >= (ulong)(newBlock ?? CurrentBlock).RowCount)
                    ThrowBadNewBlockIndex(newBlockIndex, moveCount);
            }

            return moveCount;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowBadNewBlockIndex(ulong newBlockIndex, long mc)
        {
            ThrowHelper.ThrowInvalidOperationException(
                $"newBlockIndex [{(long)newBlockIndex}] >= (ulong)CurrentBlock.RowCount [{CurrentBlock.RowCount}], mc={mc}");
        }

        /// <summary>
        /// Called when next position is outside current block.
        /// Must be pure and do not change state.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveRare(long stride, bool allowPartial, out DataBlock? newBlock, out ulong newBlockIndex)
        {
            newBlock = null;
            newBlockIndex = ulong.MaxValue;

            if (stride == 0)
                return 0;

            var cb = CurrentBlock;
            if (_source.IsDataBlock(out var localBlock, out var ds))
            {
                if (cb != localBlock && cb.IsEmptySentinel)
                {
                    return MoveInitialize(stride, allowPartial, localBlock, out newBlock, out newBlockIndex);
                }

                ThrowHelper.DebugAssert(cb == localBlock, "CurrentBlock == localBlock");

                if (BlockIndex < 0 & stride < 0) // not &&
                {
                    ThrowHelper.DebugAssert(State == CursorState.Initialized, "State == CursorState.Initialized");
                    var nextPosition = unchecked((localBlock.RowCount + stride));
                    if (nextPosition >= 0)
                    {
                        newBlockIndex = (ulong)nextPosition;
                        return stride;
                    }

                    if (allowPartial)
                    {
                        newBlockIndex = 0;
                        return -localBlock.RowCount;
                    }
                }

                if (allowPartial)
                {
                    if (BlockIndex + stride >= localBlock.RowCount)
                    {
                        var mc = (localBlock.RowCount - 1) - BlockIndex;
                        newBlockIndex = (ulong)(BlockIndex + mc);
                        return mc;
                    }
                    if (stride < 0) // cannot just use else without checks before, e.g. what if BlockIndex == -1 and stride == 0
                    {
                        newBlockIndex = 0;
                        return -BlockIndex;
                    }
                }

                return 0;
            }

            return cb.IsFull
                ? MoveBlock(ds, stride, allowPartial, out newBlock, out newBlockIndex)
                : 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private long MoveInitialize(long stride, bool allowPartial,
            DataBlock localBlock, out DataBlock? newBlock, out ulong newBlockIndex)
        {
            // Special case: the cursor was initialized before adding first
            // value when _source.Data was set to empty data block sentinel.
            // Note that it is safe to set CB here: empty -> the only block
            // that should have been instead of empty already. And we need
            // to set it due to recursion.
            CurrentBlock = localBlock;
            return Move(stride, allowPartial, out newBlock, out newBlockIndex);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private long MoveBlock(DataBlockSource<TKey> ds, long stride, bool allowPartial, out DataBlock? newBlock, out ulong newBlockIndex)
        {
            ThrowHelper.DebugAssert(stride != 0);
            ThrowHelper.DebugAssert(ds != null && ds.First.IsPresent);

            // we couldn't move withing CB, but still at the same position before the attempt

            //newBlock = null;
            //newBlockIndex = ulong.MaxValue;

            // virtual state
            var cb = CurrentBlock;
            var bi = BlockIndex;

            long remaining = stride;

            if (stride > 0)
            {
                if (bi == -1) // not initialized and not data block
                {
                    // Make the virtual state as if we were after MF
                    ThrowHelper.DebugAssert(cb.IsEmptySentinel);
                    if (AdditionalCorrectnessChecks.Enabled)
                        ThrowHelper.Assert(ds.First.IsPresent, "ds.First.IsPresent");
                    cb = ds.First.Present.Value;
                    bi = 0;
                    remaining--;
                }

                while (true)
                {
                    var availableInCb = (cb.RowCount - 1) - bi;

                    if (remaining <= availableInCb)
                    {
                        bi += (int)remaining;
                        ThrowHelper.DebugAssert(bi >= 0 && bi < cb.RowCount);
                        remaining = 0;
                        break;
                    }

                    remaining -= availableInCb;
                    bi += availableInCb;

                    // we are at the end of cb now
                    ThrowHelper.DebugAssert(bi == cb.RowCount - 1 && remaining > 0);

                    if (ds.TryGetNextBlock(cb, out var nb))
                    {
                        if (AdditionalCorrectnessChecks.Enabled)
                            ThrowHelper.Assert(nb.RowCount > 0, "nb.RowCount > 0");
                        // move to the first position of the next block
                        cb = nb;
                        bi = 0;
                        remaining--;
                    }
                    else
                    {
                        if (!allowPartial)
                            remaining = stride;
                        break;
                    }
                }
            }
            else
            {
                if (bi == -1) // not initialized and not data block
                {
                    // Make the virtual state as if we were after ML
                    ThrowHelper.DebugAssert(cb.IsEmptySentinel);
                    if (AdditionalCorrectnessChecks.Enabled)
                        ThrowHelper.Assert(ds.Last.IsPresent, "ds.First.IsPresent");
                    cb = ds.LastValueOrDefault;
                    bi = cb.RowCount - 1;
                    remaining++;
                }

                while (true)
                {
                    var availableInCb = -bi;

                    if (remaining >= availableInCb)
                    {
                        bi += (int)remaining;
                        ThrowHelper.DebugAssert(bi >= 0 && bi < cb.RowCount);
                        remaining = 0;
                        break;
                    }

                    remaining -= availableInCb;
                    bi += availableInCb;

                    // we are at the start of cb now
                    ThrowHelper.DebugAssert(bi == 0 && remaining < 0);

                    if (ds.TryGetPreviousBlock(cb, out var pb))
                    {
                        if (AdditionalCorrectnessChecks.Enabled)
                            ThrowHelper.Assert(pb.RowCount > 0, "pb.RowCount > 0");
                        // move to the last position of the previous block
                        cb = pb;
                        bi = cb.RowCount - 1;
                        remaining++;
                    }
                    else
                    {
                        if (!allowPartial)
                            remaining = stride;
                        break;
                    }
                }
            }

            var movedCount = stride - remaining;
            if (movedCount != 0)
            {
                newBlock = cb;
                newBlockIndex = (ulong)bi;
            }
            else
            {
                newBlock = null;
                newBlockIndex = ulong.MaxValue;
            }
            if (AdditionalCorrectnessChecks.Enabled && movedCount != 0 && newBlockIndex >= (ulong)(newBlock ?? CurrentBlock).RowCount)
                ThrowBadNewBlockIndex(newBlockIndex, movedCount);
            return movedCount;
        }

        [Obsolete("TODO cursor counter, throw on dispose if there are undisposed cursors")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureSourceNotDisposed()
        {
            if (_source.IsDisposed)
            {
                ThrowCursorSourceDisposed();
            }
        }

        [Obsolete("Replace in MoveAt with new sync logic")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureOrder()
        {
            if (_orderVersion != _source.OrderVersion)
            {
                ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCursorSourceDisposed()
        {
            throw new ObjectDisposedException("Cursor.Source");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveFirst()
        {
            if (State == CursorState.None)
            {
                ThrowCannotMove();
            }

            if (State == CursorState.Initialized)
            {
                return MoveNext();
            }

            ThrowHelper.DebugAssert(State == CursorState.Moving);

            var result = false;
            DataBlock? newBlock = null;

            if (_source.IsDataBlock(out var db, out var ds))
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(db == CurrentBlock);
                if (CurrentBlock.RowCount > 0)
                {
                    BlockIndex = 0;
                    result = true;
                }
            }
            else
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(ds.First.IsPresent);

                newBlock = ds.First.Present.Value;
                result = true;
            }

            TKey k = default!;
            TValue v = default!;

            if (result)
            {
                // Note: do not use _blockPosition, it's 20% slower than second cast to int
                GetCurrentKeyValue(newBlock ?? CurrentBlock, (int)0, out k, out v);
            }

            if (_source.NextOrderVersion != _orderVersion)
            {
                // MN must compare to stored version, while MA
                // must have constant order version only during the move
                newBlock?.Decrement();
                result = false;
                ThrowCannotMove();
            }

            if (result)
            {
                if (newBlock != null)
                    CurrentBlock = newBlock;
                BlockIndex = (int)0;
                _currentKey = k;
                _currentValue = v;
            }

            return result;
        }

        public bool MoveLast()
        {
            if (State == CursorState.None)
            {
                ThrowCannotMove();
            }

            if (State == CursorState.Initialized)
            {
                return MovePrevious();
            }

            ThrowHelper.DebugAssert(State == CursorState.Moving);

            var result = false;
            DataBlock? newBlock = null;
            int newBlockIndex = -1;

            if (_source.IsDataBlock(out var db, out var ds))
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(db == CurrentBlock);
                if (CurrentBlock.RowCount > 0)
                {
                    newBlockIndex = CurrentBlock.RowCount - 1;
                    result = true;
                }
            }
            else
            {
                if (AdditionalCorrectnessChecks.Enabled)
                    ThrowHelper.Assert(ds.Last.IsPresent);

                newBlock = ds.LastValueOrDefault;
                newBlockIndex = newBlock.RowCount - 1;
                result = true;
            }

            TKey k = default!;
            TValue v = default!;

            if (result)
            {
                // Note: do not use _blockPosition, it's 20% slower than second cast to int
                GetCurrentKeyValue(newBlock ?? CurrentBlock, (int)0, out k, out v); // TODO WTF(!) 0 not newBlockIndex?
            }

            if (_source.NextOrderVersion != _orderVersion)
            {
                // MN must compare to stored version, while MA
                // must have constant order version only during the move
                newBlock?.Decrement();
                result = false;
                ThrowCannotMove();
            }

            if (result)
            {
                if (newBlock != null)
                    CurrentBlock = newBlock;
                BlockIndex = newBlockIndex;
                _currentKey = k;
                _currentValue = v;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            // TODO (low) maybe some day try to optimize MN path vs generic Move(2)
            // But Move is already optimized for MN(N forward),
            // possibly this is more relevant for MP.
            return Move(stride: 1, allowPartial: false) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MovePrevious()
        {
            return Move(stride: -1, allowPartial: false) != 0;
        }

        public TKey CurrentKey
        {
            // No need to sync this access
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentKey;
        }

        // Container cursors must check order before getting CV from current block
        // but after getting the value. It helps that DataBlock cannot shrink in size,
        // only RowLength could, so we will not overrun even if order changed.

        public DataBlock CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => CurrentBlock;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public ref T GetValue<T>()
        //{
        //    ref var v = ref _currentBlock.Values._vec.DangerousGetRef<T>(_blockPosition);
        //    EnsureOrder();
        //    return ref v;
        //}

        public int CurrentBlockPosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BlockIndex;
        }

        public Series<TKey, DataBlock, BlockCursor<TKey, TValue, TContainer>> Source
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Series<TKey, DataBlock, BlockCursor<TKey, TValue, TContainer>>(Initialize());
        }

        public ValueTask<bool> MoveNextAsync()
        {
            throw new NotSupportedException("This cursor is only used as a building block of other cursors.");
        }

        public IAsyncCompleter AsyncCompleter => throw new NotSupportedException("This cursor is only used as a building block of other cursors.");

        ISeries<TKey, DataBlock> ICursor<TKey, DataBlock>.Source => Source;

        public bool IsContinuous
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor<TKey, TValue, TContainer> Initialize()
        {
            var c = this;
            c.Reset();
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor<TKey, TValue, TContainer> Clone()
        {
            var c = this;
            c.CurrentBlock?.Increment();
            return c;
        }

        ICursor<TKey, DataBlock> ICursor<TKey, DataBlock>.Clone()
        {
            return Clone();
        }

        public bool TryGetValue(TKey key, out DataBlock value)
        {
            ThrowHelper.DebugAssert(!CurrentBlock.IsDisposed || CurrentBlock.IsEmptySentinel, "!CurrentBlock.IsDisposed || ReferenceEquals(CurrentBlock, DataBlock.Empty)");

            throw new NotImplementedException();
        }

        public void Reset()
        {
            BlockIndex = -1;
            _currentKey = default;
            _orderVersion = AtomicCounter.GetCount(ref _source.OrderVersion);

            if (!_source.IsDataBlock(out _, out _))
            {
                CurrentBlock = DataBlock.Empty;
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
            if (State == CursorState.None)
            {
                ThrowHelper.ThrowInvalidOperationException("Disposing not initialized cursor.");
            }
            BlockIndex = -1;
            _currentKey = default;
            _orderVersion = AtomicCounter.GetCount(ref _source.OrderVersion);
            CurrentBlock = DataBlock.Empty;
            _source = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask();
        }

        #region Obsolete members

        public bool IsIndexed
        {
            get { throw new NotImplementedException(); }
        }

        public bool IsCompleted
        {
            get => _source.Flags.Mutability == Mutability.ReadOnly;
        }

        #endregion Obsolete members
    }
}
