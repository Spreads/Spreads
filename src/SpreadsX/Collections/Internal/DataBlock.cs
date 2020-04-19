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

        private int _rowCapacity = 0;

        /// <summary>
        /// Vec offset where data starts (where index ==0).
        /// </summary>
        protected volatile int _head = 0;

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

        public bool IsLeaf => _height == 0;

        public int RowCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCapacity;
            protected set => _rowCapacity = value;
        }
    }

    internal class DataBlockVectors : DataBlockCounters
    {
        // _rowKeys should be at the and, rarely used fields act like padding
        private DataBlock? _previousBlock;
        private DataBlock? _nextBlock;
        private RetainedVec[]? _columns;
        private RetainedVec _columnKeys;
        private RetainedVec _values;
        private RetainedVec _rowKeys;

        internal RetainedVec RowKeys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowKeys;
            set => _rowKeys = value;
        }

        internal RetainedVec ColumnKeys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _columnKeys;
            set => _columnKeys = value;
        }

        internal RetainedVec Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _values;
            set => _values = value;
        }

        internal RetainedVec[]? Columns
        {
            get { return _columns; }
            set => _columns = value;
        }

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
                ThrowHelper.DebugAssert(value == null || (value.Height == Height && value.RowCount > 0));
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
                ThrowHelper.DebugAssert(value == null || (value?.Height == Height && value.RowCount > 0));
                _nextBlock = value;
            }
        }
    }

    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame blocks.
    /// </summary>
    /// <remarks>
    /// This is thread-unsafe storage. Callers should use some locking mechanism.
    /// </remarks>
    internal sealed unsafe partial class DataBlock : DataBlockVectors, IRefCounted, IDisposable
    {
        /// <summary>
        /// We need this as a sentinel with RowLength == 0 in cursor
        /// to not penalize fast path of single-block containers.
        /// </summary>
        internal static readonly DataBlock Empty = new DataBlock {RowCount = 0};

        internal static ref DataBlock NullRef => ref Unsafe.AsRef<DataBlock>((void*) IntPtr.Zero);

        // TODO Review pools sizes, move them to settings. For series each DataBlock has 2x PrivateMemory instances, for which we have 256 pooled per core.
        private static readonly ObjectPool<DataBlock> ObjectPool = new ObjectPool<DataBlock>(() => new DataBlock(), perCoreSize: 256);

        private DataBlock()
        {
            // pool returns disposed data blocks
            AtomicCounter.Dispose(ref _refCount);
        }

        public int ColumnCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ColumnKeys.Length;
        }

        /// <summary>
        /// True if <see cref="RowCount"/> is equal to <see cref="RowCapacity"/>.
        /// </summary>
        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RowCount == RowKeys.Length;
        }

        /// <summary>
        /// This data block is <see cref="DataBlock.Empty"/>.
        /// </summary>
        public bool IsEmptySentinel
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ReferenceEquals(this, Empty);
        }

        /// <summary>
        /// Move all content into a new block, this block has all default values
        /// </summary>
        /// <returns></returns>
        public DataBlock MoveInto()
        {
            DataBlock destination = ObjectPool.Rent()!;
            MoveInto(destination);
            return destination;
        }

        /// <summary>
        /// Move all content into a block, this block has all default values
        /// </summary>
        /// <returns></returns>
        public void MoveInto(DataBlock destination)
        {
            destination.RowCount = RowCount;
            destination.ColumnCount = ColumnCount;
            destination._refCount = _refCount;
            destination.Height = Height;
            destination.PreviousBlock = PreviousBlock;
            destination.NextBlock = NextBlock;
            destination.Columns = Columns;
            destination.ColumnKeys = ColumnKeys;
            destination.RowKeys = RowKeys;
            destination.Values = Values;
            destination.RowCapacity = RowCapacity;
            destination._head = _head;

            RowCount = default;
            ColumnCount = default;
            _refCount = default;
            Height = default;
            PreviousBlock = default;
            NextBlock = default;
            Columns = default;
            ColumnKeys = default;
            RowKeys = default;
            Values = default;
            RowCapacity = default;
            _head = default;
        }

        #region Lifecycle

        // TODO delete this method
        [Obsolete("Use container-specific factories, e.g. CreateForSeries")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DataBlock Create(RetainedVec rowIndex = default, RetainedVec values = default,
            RetainedVec[]? columns = null, int rowLength = -1)
        {
            var block = ObjectPool.Rent();
            block.EnsureDisposed();

            var rowCapacity = -1;

            if (rowIndex != default)
            {
                block.RowKeys = rowIndex;
                rowCapacity = rowIndex.Length;
            }

            if (values != default)
            {
                block.Values = values;
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

                block.Columns = columns;
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
            ThrowHelper.DebugAssert(RowKeys == default);
            ThrowHelper.DebugAssert(ColumnKeys == default);
            ThrowHelper.DebugAssert(Values == default);
            ThrowHelper.DebugAssert(Columns == default);
            ThrowHelper.DebugAssert(NextBlock == default);
            ThrowHelper.DebugAssert(PreviousBlock == default);
        }

        internal void Dispose(bool disposing)
        {
            if (this == Empty)
                return;

            if (IsDisposed)
                ThrowDisposed();

            AtomicCounter.Dispose(ref _refCount); // TODO atomic try dispose

            if (!disposing)
                WarnFinalizing();

            if (Height > 0)
                DisposeNode();

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

            if (RowKeys != default)
            {
                RowKeys.Dispose();
                RowKeys = default;
            }

            if (ColumnKeys != default)
            {
                ColumnKeys.Dispose();
                ColumnKeys = default;
            }

            if (Values != default)
            {
                Values.Dispose();
                Values = default;
            }

            if (Columns != null)
            {
                for (int i = 0; i < Columns.Length; i++)
                {
                    Columns[i].Dispose(); // does _memoryOwner?.Increment => works for default RV
                    Columns = default;
                }
            }

            var returned = ObjectPool.Return(this);
            if (!returned)
                GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void DisposeNode()
        {
            for (int i = 0; i < RowCount; i++)
            {
                var node = Values.UnsafeReadUnaligned<DataBlock>(i);
                node.Decrement();
            }
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
        }

        ~DataBlock()
        {
#if DEBUG
            ThrowHelper.ThrowInvalidOperationException("Finalizing DataBlock");
#endif
            Dispose(false);
        }

        #endregion Lifecycle

        // TODO remove/rework these checks

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
                if (Values == default)
                {
                    return false;
                }

                if (Columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in Columns)
                {
                    if (vectorStorage._memoryOwner == Values._memoryOwner)
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
                if (Values == default)
                {
                    return false;
                }

                if (Columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in Columns)
                {
                    if (vectorStorage._memoryOwner != Values._memoryOwner)
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
                if (Columns != null)
                {
                    if (Columns.Length > 0)
                    {
                        colLength = Columns[0].Length;
                    }
                    else
                    {
                        // should not have empty columns array
                        return false;
                    }

                    if (Columns.Length > 1)
                    {
                        for (int i = 1; i < Columns.Length; i++)
                        {
                            if (colLength != Columns[0].Length)
                            {
                                return false;
                            }
                        }
                    }
                }

                // shared source by any column
                // ReSharper disable once AssignNullToNotNullAttribute : colLength >= 0 guarantees _columns != null
                if (colLength >= 0 && Values != default && Columns.All(c => c._memoryOwner != Values._memoryOwner))
                {
                    // have _value set without shared source, that is not supported
                    return false;
                }

                if (colLength == -1 && Values != default)
                {
                    colLength = Values.Length;
                }

                if (RowKeys != default && RowKeys.Length != colLength)
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
                if (Values == default)
                {
                    return false;
                }

                if (!(Columns != null && Columns.Length > 0))
                {
                    return false;
                }

                // var len = -1;
                foreach (var vectorStorage in Columns)
                {
                    if (vectorStorage._memoryOwner != Values._memoryOwner)
                    {
                        return false;
                    }
                    else
                    {
                        Debug.Assert(vectorStorage.RuntimeTypeId == Values.RuntimeTypeId);
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