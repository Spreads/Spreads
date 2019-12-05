// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
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
                _currentBlockStorage?.Decrement();
                _currentBlockStorage = value;
                value?.Increment();
                
            }
#pragma warning restore 618
        }

        internal int _blockIndex;

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

        internal TValue _currentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor(TContainer source)
        {
            _source = source;
            _blockIndex = -1;
#pragma warning disable 618
            _currentBlockStorage = null;
#pragma warning restore 618
            _orderVersion = AtomicCounter.GetCount(ref _source.OrderVersion); // TODO
            _currentKey = default!;
            _currentValue = default!;
            Debug.Assert(source.Data != null, "source.Data != null: must be DataBlock.Empty instead of null");
            if (source.IsDataBlock(out var db, out _))
            {
                CurrentBlock = db;
            }
            else
            {
                CurrentBlock = DataBlock.Empty;
            }
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

                return _blockIndex >= 0 ? CursorState.Moving : CursorState.Initialized;
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
            bool found;
            int nextPosition;
            DataBlock nextBlock;
            TValue v = default!;
            var sw = new SpinWait();

        RETRY:

            var version = _source.Version;
            {
                found = _source.TryFindBlockAt(ref key, direction, out nextBlock, out nextPosition, updateDataBlock: false);
                if (found)
                {
                    if (typeof(TContainer) == typeof(Series<TKey, TValue>))
                    {
                        // TODO review. Via _vec is much faster but we assume that stride is 1
                        v = CurrentBlock.DangerousValueRef<TValue>(nextPosition);
                    }
                }
            }

            if (_source.NextVersion != version)
            {
                // See Move comments
                EnsureSourceNotDisposed();
                EnsureOrder();
                sw.SpinOnce();
                goto RETRY;
            }

            if (found)
            {
                _blockIndex = nextPosition;
                _currentKey = key;
                _currentValue = v;
                if (nextBlock != null)
                {
                    CurrentBlock = nextBlock;
                }
            }

            return found;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public long Move(long stride, bool allowPartial)
        {
            long mc;
            ulong nextPosition;
            DataBlock nextBlock = null;
            TKey k;
            TValue v = default;
            var sw = new SpinWait();
        SYNC:

            var version = _source.Version;
            {
                // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride < 0). // This case is rare.
                // Uninitialized multi-block case goes to rare as well as uninitialized MP
                nextPosition = unchecked((ulong)(_blockIndex + stride)); // long.Max + int.Max < ulong.Max
                if (nextPosition < (ulong)CurrentBlock.RowCount)
                {
                    mc = stride;
                }
                else
                {
                    mc = MoveRare(stride, allowPartial, ref nextPosition, ref nextBlock);
                }

                k = CurrentBlock.DangerousRowKeyRef<TKey>((int)nextPosition); // Note: do not use _blockPosition, it's 20% slower than second cast to int

                if (typeof(TContainer) == typeof(Series<TKey, TValue>))
                {
                    // TODO review. Via _vec is much faster but we assume that stride is 1
                    v = CurrentBlock.DangerousValueRef<TValue>((int)nextPosition);
                }
                //else // TODO value getter for other containers or they could do in CV getter but need to call EnsureOrder after reading value.
                //{
                //    v = default; // _source.GetValue(_currentBlock, (int)nextPosition);
                //}
            }

            if (_source.NextVersion != version)
            {
                // TODO set different versions on source Dispose and _currentBlock.RowLength  to 0, MoveRare has disposal check at the beginning
                EnsureSourceNotDisposed();

                // TODO review if this is logically correct to check order version only here? We do check is again in value getter later
                EnsureOrder();
                sw.SpinOnce();
                goto SYNC;
            }

            if (mc != 0)
            {
                _blockIndex = (int)nextPosition;
                _currentKey = k;
                _currentValue = v;
                if (nextBlock != null)
                {
                    CurrentBlock = nextBlock;
                }
            }

            return mc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureSourceNotDisposed()
        {
            if (_source.IsDisposed)
            {
                ThrowCursorSourceDisposed();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureOrder()
        {
            // this should be false for all cases

            if (_orderVersion != AtomicCounter.GetCount(ref _source.OrderVersion))
            {
                ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
            }
        }

        /// <summary>
        /// Called when next position is outside current block. Must be pure and do not change state.
        /// </summary>
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public long MoveRare(long stride, bool allowPartial, ref ulong nextPos, ref DataBlock nextBlock)
        {
            EnsureSourceNotDisposed();

            var localBlock = CurrentBlock;

            // var nextPosition = unchecked((ulong)(_blockPosition + stride)); // long.Max + int.Max < ulong.Max
            // Debug.Assert(nextPosition >= (ulong)localBlock.RowLength);

            if (_source.IsDataBlock(out var db, out var ds))
            {
                if (_blockIndex < 0 && stride < 0)
                {
                    Debug.Assert(State == CursorState.Initialized);
                    var nextPosition = unchecked((localBlock.RowCount + stride));
                    if (nextPosition >= 0)
                    {
                        nextPos = (ulong)nextPosition;
                        return stride;
                    }

                    if (allowPartial)
                    {
                        nextPos = 0;
                        return -localBlock.RowCount;
                    }
                }

                if (allowPartial)
                {
                    // TODO test for edge cases
                    if (_blockIndex + stride >= localBlock.RowCount)
                    {
                        var mc = (localBlock.RowCount - 1) - _blockIndex;
                        nextPos = (ulong)(_blockIndex + mc);
                        return mc;
                    }
                    if (stride < 0) // cannot just use else without checks before, e.g. what if _blockPosition == -1 and stride == 0
                    {
                        {
                            var mc = _blockIndex;
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCursorSourceDisposed()
        {
            throw new ObjectDisposedException("Cursor.Source");
        }

        [Pure]
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
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
            if (_source.IsDataBlock(out var db, out var ds))
            {
                if (CurrentBlock.RowCount > 0)
                {
                    _blockIndex = 0;
                    return true;
                }

                return false;
            }
            throw new NotImplementedException();
        }

        public bool MoveLast()
        {
            // TODO sync
            if (_source.IsDataBlock(out var db, out var ds))
            {
                if (CurrentBlock.RowCount > 0)
                {
                    _blockIndex = CurrentBlock.RowCount - 1;
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
            get => _blockIndex;
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
            throw new NotImplementedException();
        }

        public void Reset()
        {
            _blockIndex = -1;
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
            _blockIndex = -1;
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
