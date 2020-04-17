// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using Spreads.Threading;

namespace Spreads.Collections.Internal
{
    internal class DataBlockCounters
    {
        /// <summary>
        /// Number of data rows.
        /// </summary>
        private volatile int _rowCount;

        /// <summary>
        /// Logical column count: 1 for Series, N for Panel (with _columns = null & row-major values) and Frames.
        /// </summary>
        private int _columnCount;

        /// <summary>
        /// Primarily for cursors and circular implementation.
        /// We keep blocks used by cursors, which must
        /// decrement a block ref count when they no longer need it.
        /// </summary>
        protected int _refCount;

        private int _height;

        public int RowCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCount;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            protected set => _rowCount = value;
        }

        public int ColumnCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _columnCount;
            protected set => _columnCount = value;
        }

        public int Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _height;
            protected set => _height = value;
        }
    }

    internal class DataBlockPadding : DataBlockCounters
    {
        // private Padding40 _padding;
    }

    internal class DataBlockVectors : DataBlockPadding
    {
        // 4x8 references = 32 + _columnKeys.Size = 32 => 64 bytes of padding
        // before _rowKeys and _values from _rowCount
        private DataBlock? _previousBlock;
        private DataBlock? _nextBlock;
        private DataBlock? _lastBlock;

        protected RetainedVec[]? _columns;

        protected RetainedVec _columnKeys;

        protected RetainedVec _rowKeys;
        protected RetainedVec _values;

        /// <summary>
        /// Fast path to get the previous block from the current one.
        /// </summary>
        /// <remarks>
        /// See remarks in <see cref="NextBlock"/>
        /// </remarks>
        /// <seealso cref="NextBlock"/>
        public DataBlock? PreviousBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _previousBlock;
            protected set
            {
                ThrowHelper.Assert(value?.Height == Height && value.RowCount > 0);
                _previousBlock = value;
            }
        }

        /// <summary>
        /// Fast path to get the next block from the current one.
        /// </summary>
        /// <remarks>
        /// A null value does not mean that there is no next block but
        /// it just means that we cannot get it in a super fast way (whatever
        /// this means depends on implementation).
        /// </remarks>
        /// <seealso cref="PreviousBlock"/>
        public DataBlock? NextBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nextBlock;
            protected set
            {
                ThrowHelper.Assert(value?.Height == Height && value.RowCount > 0);
                _nextBlock = value;
            }
        }

        /// <summary>
        /// When this block has height > 0, i.e. is a block of blocks, this
        /// property contains the very last block with height == 0.
        /// This could go several layers deep, not only last block of this block.
        /// </summary>
        public DataBlock? LastBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _lastBlock;
            protected set
            {
                ThrowHelper.DebugAssert(value?.Height == 0 && value.RowCount > 0);
                _lastBlock = value;
            }
        }
    }

    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame blocks.
    /// </summary>
    /// <remarks>
    /// This is thread-unsafe storage. Callers should use some locking mechanism.
    /// </remarks>
    internal sealed partial class DataBlock : DataBlockVectors, IRefCounted, IDisposable
    {
        /// <summary>
        /// We need this as a sentinel with RowLength == 0 in cursor
        /// to not penalize fast path of single-block containers.
        /// </summary>
        internal static readonly DataBlock Empty = new DataBlock {RowCount = 0};

        // TODO Review pools sizes, move them to settings. For series each DataBlock has 2x PrivateMemory instances, for which we have 256 pooled per core.
        private static readonly ObjectPool<DataBlock> ObjectPool = new ObjectPool<DataBlock>(() => new DataBlock(), perCoreSize: 256);

        [Obsolete("Use only in tests")]
        internal RetainedVec RowKeys => _rowKeys;

        [Obsolete("Use only in tests")]
        internal RetainedVec ColumnKeys => _columnKeys;

        [Obsolete("Use only in tests")]
        internal RetainedVec Values => _values;

        [Obsolete("Use only in tests")]
        internal RetainedVec[]? Columns => _columns;

        // TODO this is only for MW, and we should pay the cost only in that case

        private int _rowCapacity = 0;

        /// <summary>
        /// Vec offset where data starts (where index ==0).
        /// </summary>
        private volatile int _head = 0;

        private DataBlock()
        {
            // pool returns disposed data blocks
            AtomicCounter.Dispose(ref _refCount);
        }

        #region Lifecycle

        // TODO delete this method
        [Obsolete("Use container-specific factories, e.g. SeriesCreate")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DataBlock Create(RetainedVec rowIndex = default, RetainedVec values = default,
            RetainedVec[]? columns = null, int rowLength = -1)
        {
            var block = ObjectPool.Rent();
            block.EnsureDisposed();

            var rowCapacity = -1;

            if (rowIndex != default)
            {
                block._rowKeys = rowIndex;
                rowCapacity = rowIndex.Length;
            }

            if (values != default)
            {
                block._values = values;
                if (rowCapacity >= 0 && values.Length < rowCapacity)
                {
                    ThrowHelper.ThrowArgumentException("");
                }
                else
                {
                    rowCapacity = values.Length;
                }

                rowCapacity = Math.Min(rowCapacity, values.Length);
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
                    rowCapacity = Math.Min(rowCapacity, column.Length);
                }
            }

            if (rowLength == -1)
            {
                block.RowCount = rowCapacity;
            }
            else
            {
                if ((uint) rowLength > rowCapacity)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("rowLength");
                }
                else
                {
                    block.RowCount = rowLength;
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
            get => AtomicCounter.GetIsDisposed(ref _refCount);
        }

        [Conditional("DEBUG")]
        private void EnsureDisposed()
        {
            ThrowHelper.DebugAssert(AtomicCounter.GetIsDisposed(ref _refCount));
            ThrowHelper.DebugAssert(RowCount == default);
            ThrowHelper.DebugAssert(ColumnCount == default);
            ThrowHelper.DebugAssert(_head == default);
            ThrowHelper.DebugAssert(_rowKeys == default);
            ThrowHelper.DebugAssert(_values == default);
            ThrowHelper.DebugAssert(_columns == default);
            ThrowHelper.DebugAssert(NextBlock == default);
            ThrowHelper.DebugAssert(PreviousBlock == default);
            ThrowHelper.DebugAssert(LastBlock == default);
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

            RowCount = default;
            ColumnCount = default;
            _head = default;

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

        public int RowCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCapacity;
        }

        public int ColumnCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _columnKeys.Length;
        }

        /// <summary>
        /// True if <see cref="RowCount"/> is equal to <see cref="RowCapacity"/>.
        /// </summary>
        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RowCount == _rowKeys.Length;
        }

        /// <summary>
        /// This data block is <see cref="DataBlock.Empty"/>.
        /// </summary>
        public bool IsEmptySentinel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ReferenceEquals(this, Empty);
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
                        colLength = _columns[0].Length;
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
                            if (colLength != _columns[0].Length)
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
                    colLength = _values.Length;
                }

                if (_rowKeys != default && _rowKeys.Length != colLength)
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
                        Debug.Assert(vectorStorage.RuntimeTypeId == _values.RuntimeTypeId);
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