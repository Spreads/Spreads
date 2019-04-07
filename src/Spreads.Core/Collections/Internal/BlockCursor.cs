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

    //public interface IDataBlockValueGetter<TValue>
    //{
    //    TValue GetValue(DataBlock block, int rowIndex);
    //}

    /// <summary>
    /// <see cref="Collections.Experimental.Series{TKey,TValue}"/> cursor implementation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)] // This struct will be aligned to IntPtr.Size bytes because it has references, but small fields could be packed within 8 bytes.
    internal struct BlockCursor<TKey, TValue, TContainer> : ICursorNew<TKey, TValue>, ISpecializedCursor<TKey, DataBlock, BlockCursor<TKey, TValue, TContainer>>
        where TContainer : BaseContainer<TKey> // , IDataBlockValueGetter<TValue>
    {
        internal TContainer _source;

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

        internal TValue _currentValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BlockCursor(TContainer source)
        {
            _source = source;
            _blockPosition = -1;
            _currentBlock = source.DataSource == null ? source.DataBlock : DataBlock.Empty;
            _orderVersion = _source._orderVersion.CountOrZero; // TODO
            _currentKey = default;
            _currentValue = default;
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
            get => _source._comparer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveAt(TKey key, Lookup direction)
        {
            bool found;
            int nextPosition;
            DataBlock nextBlock;
            TValue v = default;
            var sw = new SpinWait();

        RETRY:

            var version = _source.Version;
            {
                found = _source.TryFindBlockAt(ref key, direction, out nextBlock, out nextPosition, updateDataBlock: false);
                if (found)
                {
                    if (typeof(TContainer) == typeof(Collections.Experimental.Series<TKey, TValue>))
                    {
                        Debug.Assert(_currentBlock.Values._stride == 1);
                        // TODO review. Via _vec is much faster but we assume that stride is 1
                        v = _currentBlock.Values.DangerousGetRef<TValue>(nextPosition);
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
                _blockPosition = nextPosition;
                _currentKey = key;
                _currentValue = v;
                if (nextBlock != null)
                {
                    _currentBlock = nextBlock;
                }
            }

            return found;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryMoveNextBatch(out Segment<TKey, TValue> batch)
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
            TValue v = default;
            var sw = new SpinWait();
        SYNC:

            var version = _source.Version;
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

                Debug.Assert(_currentBlock.RowIndex._stride == 1);
                k = _currentBlock.RowIndex.DangerousGetRef<TKey>((int)nextPosition); // Note: do not use _blockPosition, it's 20% slower than second cast to int

                if (typeof(TContainer) == typeof(Collections.Experimental.Series<TKey, TValue>))
                {
                    Debug.Assert(_currentBlock.Values._stride == 1);

                    // TODO review. Via _vec is much faster but we assume that stride is 1
                    v = _currentBlock.Values.DangerousGetRef<TValue>((int)nextPosition);
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
                _blockPosition = (int)nextPosition;
                _currentKey = k;
                _currentValue = v;
                if (nextBlock != null)
                {
                    _currentBlock = nextBlock;
                }
            }

            return mc;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureSourceNotDisposed()
        {
            if (_source.DataBlock == null)
            {
                ThrowCursorSourceDisposed();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureOrder()
        {
            // this should be false for all cases

            if (_orderVersion != _source._orderVersion.CountOrZero)
            {
                ThrowHelper.ThrowOutOfOrderKeyException(_currentKey);
            }
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
            EnsureSourceNotDisposed();

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCursorSourceDisposed()
        {
            throw new ObjectDisposedException("Cursor.Source");
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

        // Container cursors must check order before getting CV from current block
        // but after getting the value. It helps that DataBlock cannot shrink in size,
        // only RowLength could, so we will not overrun even if order changed.

        public DataBlock CurrentValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentBlock;
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
            get => _blockPosition;
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
            _orderVersion = _source._orderVersion.CountOrZero;

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
            // DataBlock is not ref counted but just owned by container.
            Reset();
            _source = null;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return new ValueTask();
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