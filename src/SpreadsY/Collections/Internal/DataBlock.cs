// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Algorithms;
using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using Spreads.Threading;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Collections.Internal
{
    [StructLayout((LayoutKind.Sequential))]
    internal class DataBlockCounters
    {
        /// <summary>
        /// Number of data rows.
        /// </summary>
        protected volatile int _rowCount = -1;
        protected volatile int _columnCount = -1;

        /// <summary>
        /// Primarily for cursors and circular implementation.
        /// We do not release blocks used by cursors, which must
        /// decrement a block ref count when they no longer need it.
        /// </summary>
        protected int _refCount;
    }

    [StructLayout((LayoutKind.Sequential))]
    internal class DataBlockCountersPadding : DataBlockCounters
    {
        // private Padding40 _padding;
    }

    [StructLayout((LayoutKind.Sequential))]
    internal class DataBlockVectors : DataBlockCountersPadding
    {
        protected RetainedVec _rowKeys;
#pragma warning disable 649
        protected RetainedVec _columnKeys;
#pragma warning restore 649

        protected RetainedVec _values; // TODO (review) could be valuesOrColumnIndex instead of storing ColumnIndex in _columns[0]

        protected RetainedVec[]? _columns;

        protected ref RetainedVec GeyKeys(int isColumn)
        {
            Debug.Assert(isColumn == 0 || isColumn == 1);
            return ref Unsafe.Add(ref _rowKeys, isColumn);
        }
    }

    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame blocks.
    /// </summary>
    /// <remarks>
    /// This is thread-unsafe storage. Callers should use some locking mechanism.
    /// </remarks>
    [StructLayout((LayoutKind.Sequential))]
    internal sealed partial class DataBlock : DataBlockVectors, IRefCounted, IDisposable
    {
        /// <summary>
        /// We need this as a sentinel with RowLength == 0 in cursor to not penalize fast path of single-block containers.
        /// </summary>
        internal static readonly DataBlock Empty = new DataBlock {_rowCount = 0};

        // For series each DataBlock has 2x PrivateMemory instances, for which we have 256 pooled per core.
        // TODO Review pools sizes, move them to settings

        private static readonly ObjectPool<DataBlock> ObjectPool = new ObjectPool<DataBlock>(() => new DataBlock(), 256);

        [Obsolete("Use only in tests")]
        internal RetainedVec RowKeys => _rowKeys;

        [Obsolete("Use only in tests")]
        internal RetainedVec Values => _values;

        [Obsolete("Use only in tests")]
        internal RetainedVec[]? Columns => _columns;

        /// <summary>
        /// Fast path to get the next block from the current one.
        /// </summary>
        /// <remarks>
        /// A null value does not mean that there is no next block but
        /// it just means that we cannot get it in a super fast way (whatever
        /// this means depends on implementation).
        /// </remarks>
        /// <seealso cref="PreviousBlock"/>
        internal DataBlock? NextBlock;

        /// <summary>
        /// Fast path to get the previous block from the current one.
        /// </summary>
        /// <remarks>
        /// See remarks in <see cref="NextBlock"/>
        /// </remarks>
        /// <seealso cref="NextBlock"/>
        internal DataBlock? PreviousBlock;

        /// <summary>
        /// Vec offset where data starts (where index ==0).
        /// </summary>
        private volatile int _head = -1;

        private volatile bool _isFull = false;

        private DataBlock()
        {
            AtomicCounter.Dispose(ref _refCount);
        }

        #region Lifecycle

        // TODO delete this method
        [Obsolete("Use container-specific factories, e.g. SeriesCreate")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DataBlock Create(RetainedVec rowIndex = default, RetainedVec values = default, RetainedVec[]? columns = null, int rowLength = -1)
        {
            var block = ObjectPool.Rent();
            block.EnsureDisposed();

            var rowCapacity = -1;

            if (rowIndex != default)
            {
                block._rowKeys = rowIndex;
                rowCapacity = rowIndex.Vec.Length;
            }

            if (values != default)
            {
                block._values = values;
                if (rowCapacity >= 0 && values.Vec.Length < rowCapacity)
                {
                    ThrowHelper.ThrowArgumentException("");
                }
                else
                {
                    rowCapacity = values.Vec.Length;
                }

                rowCapacity = Math.Min(rowCapacity, values.Vec.Length);
            }

            if (columns != null)
            {
                if (columns.Length == 0)
                {
                    ThrowHelper.ThrowArgumentException("Empty columns array. Pass null instead.");
                }

                block._columns = columns;
                foreach (var column in columns)
                {
                    rowCapacity = Math.Min(rowCapacity, column.Vec.Length);
                }
            }

            if (rowLength == -1)
            {
                block._rowCount = rowCapacity;
            }
            else
            {
                if ((uint) rowLength > rowCapacity)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("rowLength");
                }
                else
                {
                    block._rowCount = rowLength;
                }
            }

            block._head = 0;
            block._refCount = 0;

            ThrowHelper.DebugAssert(!block.IsDisposed, "!block.IsDisposed");

            return block;
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetIsDisposed(ref _refCount) || RowCount < 0;
        }

        [Conditional("DEBUG")]
        private void EnsureDisposed()
        {
            Debug.Assert(AtomicCounter.GetIsDisposed(ref _refCount));
            Debug.Assert(_rowCount == -1);
            Debug.Assert(_head == -1);
            Debug.Assert(_rowKeys == default);
            Debug.Assert(_values == default);
            Debug.Assert(_columns == null);
            Debug.Assert(NextBlock == null);
            Debug.Assert(PreviousBlock == null);
        }

        private void Dispose(bool disposing)
        {
            if (this == Empty)
            {
                return;
            }

            if (IsDisposed)
            {
                ThrowDisposed();
            }

            AtomicCounter.Dispose(ref _refCount); // TODO atomic try dispose

            if (!disposing)
            {
                WarnFinalizing();
            }

            _rowCount = -1;
            _columnCount = -1;
            _head = -1;

            if (NextBlock != null)
            {
                Debug.Assert(NextBlock.PreviousBlock == this);
                NextBlock.PreviousBlock = null;
                NextBlock = null;
            }

            if (PreviousBlock != null)
            {
                Debug.Assert(PreviousBlock.NextBlock == this);
                PreviousBlock.NextBlock = null;
                PreviousBlock = null;
            }

            if (_rowKeys != default)
            {
                _rowKeys.Dispose();
                _rowKeys = default;
            }
            
            if (_columnKeys != default)
            {
                _columnKeys.Dispose();
                _columnKeys = default;
            }

            if (_values != default)
            {
                _values.Dispose();
                _values = default;
            }

            if (_columns != null)
            {
                for (int i = 0; i < _columns.Length; i++)
                {
                    _columns[i].Dispose(); // does _memoryOwner?.Increment => works for default RV
                    _columns = default;
                }
            }
            
            ObjectPool.Return(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WarnFinalizing()
        {
            Trace.TraceWarning("Finalizing DataBlock. It must be properly disposed.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(DataBlock));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataBlock()
        {
#if DEBUG
            ThrowHelper.ThrowInvalidOperationException("Finalizing DataBlock");
#endif
            Dispose(false);
        }

        #endregion Lifecycle

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexToOffset(int index)
        {
            return RingVecUtil.IndexToOffset(index, _head, _rowCount);
        }

        private int OffsetToIndex(int offset)
        {
            throw new NotImplementedException();
        }

        public int RowCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCount;
        }

        public int RowCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowKeys.Vec.Length;
        }

        /// <summary>
        /// True if <see cref="RowCount"/> is equal to <see cref="RowCapacity"/>.
        /// </summary>
        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCount == _rowKeys.Vec.Length;
        }

        /// <summary>
        /// This data block is <see cref="DataBlock.Empty"/>.
        /// </summary>
        public bool IsEmptySentinel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ReferenceEquals(this, Empty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousRowKey<T>(int index)
        {
            int offset = RingVecUtil.IndexToOffset(index, _head, _rowCount);
            return _rowKeys.Vec.DangerousGetUnaligned<T>(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousValue<T>(int index)
        {
            // TODO review if we will use ring buffer structure for something other than Series.DataSource
            int offset = GetSeriesOffset<T>(index);
            return _values.Vec.DangerousGetUnaligned<T>(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int LookupKey<T>(ref T key, Lookup lookup, KeyComparer<T> comparer = default)
        {
            if (lookup == Lookup.EQ)
                return SearchKey(key, comparer);

            if (_head + _rowCount <= _rowKeys.Vec.Length)
            {
                return _head + VectorSearch.SortedLookup(ref _rowKeys.UnsafeGetRef<T>(),
                    _head, _rowCount, ref key, lookup, comparer);
            }

            return LookupKeySlower(ref key, lookup, comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int LookupKeySlower<T>(ref T key, Lookup lookup, KeyComparer<T> comparer)
        {
            throw new NotImplementedException();
            // This should be pretty fast vs. manually managing edge cases on
            // the wrap boundary. TODO test
            // var wrappedVec = new RingVec<T>(_rowKeys.Vec, _head, _rowCount);
            // return VectorSearch.SortedLookup(ref wrappedVec, 0,
            //     _rowCount, ref key, lookup, comparer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int SearchKey<T>(T key, KeyComparer<T> comparer = default)
        {
            if (_head + _rowCount <= _rowKeys.Vec.Length)
                return VectorSearch.SortedSearch(ref _rowKeys.UnsafeGetRef<T>(), _head, _rowCount, key, comparer);

            // TODO for EQ search we do not need a wrapper, we just need to
            // decide where to search based on comparison with the last offset

            return SearchKeySlower(key, comparer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int SearchKeySlower<T>(T key, KeyComparer<T> comparer)
        {
            throw new NotImplementedException();
            // This should be pretty fast vs. manually managing edge cases on
            // the wrap boundary. TODO test
            // var wrappedVec = new RingVec<T>(_rowKeys.Vec, _head, _rowCount);
            // return VectorSearch.SortedSearch(ref wrappedVec, 0,
            //     _rowCount, key, comparer);
        }

        #region Structure check

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotSentinel()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (IsEmptySentinel)
                {
                    DoThrow();
                }

                void DoThrow()
                {
                    ThrowHelper.ThrowInvalidOperationException("DataBlock.Empty must only be used as sentinel in DataBlock cursor");
                }
            }
        }

        // TODO do not pretend to make it right at the first take, will review and redefine
        // TODO make tests
        public bool IsAnyColumnShared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_values == default)
                {
                    return false;
                }

                if (_columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memoryOwner == _values._memoryOwner)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsAllColumnsShared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_values == default)
                {
                    return false;
                }

                if (_columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memoryOwner != _values._memoryOwner)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // TODO review, need specification
        // 1. Values and columns are both not null only when structural sharing
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsDisposed)
                {
                    return false;
                }

                var colLength = -1;
                if (_columns != null)
                {
                    if (_columns.Length > 0)
                    {
                        colLength = _columns[0].Vec.Length;
                    }
                    else
                    {
                        // should not have empty columns array
                        return false;
                    }

                    if (_columns.Length > 1)
                    {
                        for (int i = 1; i < _columns.Length; i++)
                        {
                            if (colLength != _columns[0].Vec.Length)
                            {
                                return false;
                            }
                        }
                    }
                }

                // shared source by any column
                // ReSharper disable once AssignNullToNotNullAttribute : colLength >= 0 guarantees _columns != null
                if (colLength >= 0 && _values != default && _columns.All(c => c._memoryOwner != _values._memoryOwner))
                {
                    // have _value set without shared source, that is not supported
                    return false;
                }

                if (colLength == -1 && _values != default)
                {
                    colLength = _values.Vec.Length;
                }

                if (_rowKeys != default && _rowKeys.Vec.Length != colLength)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Matrix is stored row-wise if mutable and optionally (TODO) column-wise when immutable
        /// </summary>
        public bool IsPureMatrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_values == default)
                {
                    return false;
                }

                if (!(_columns != null && _columns.Length > 0))
                {
                    return false;
                }

                // var len = -1;
                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memoryOwner != _values._memoryOwner)
                    {
                        return false;
                    }
                    else
                    {
                        Debug.Assert(vectorStorage.Vec.RuntimeTypeId == _values.Vec.RuntimeTypeId);
                    }
                }

                return true;
            }
        }

        #endregion Structure check

        public int ReferenceCount => AtomicCounter.GetCount(ref _refCount);

        public int Increment()
        {
            return AtomicCounter.Increment(ref _refCount);
        }

        public int Decrement()
        {
            return AtomicCounter.Decrement(ref _refCount);
        }
    }

    internal class DataBlockLayoutException : SpreadsException
    {
        public DataBlockLayoutException(string message) : base(message)
        {
        }
    }
}