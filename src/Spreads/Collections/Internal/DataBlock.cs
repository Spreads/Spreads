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
    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame blocks.
    /// </summary>
    /// <remarks>
    /// This is thread-unsafe storage. Callers should use some synchronization mechanism.
    /// </remarks>
    internal sealed unsafe partial class DataBlock : IRefCounted
    {
        /// <summary>
        /// We need this as a sentinel with RowLength == 0 in cursor
        /// to not penalize fast path of single-block containers.
        /// </summary>
        internal static readonly DataBlock Empty = new();

        internal static ref DataBlock NullRef => ref Unsafe.NullRef<DataBlock>();

        // TODO Review pools sizes, move them to settings. For series each DataBlock has 2x PrivateMemory instances, for which we have 256 pooled per core.
        private static readonly ObjectPool<DataBlock> ObjectPool = new ObjectPool<DataBlock>(() => new DataBlock(), perCoreSize: 256);

        private DataBlock()
        {
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
            destination.Lo = Lo;
            destination.Hi = Hi;
            destination.RowCapacity = RowCapacity;
            destination.ColumnCount = ColumnCount;
            destination.Height = Height;
            destination._refCount = _refCount;
            destination.PreviousBlock = PreviousBlock;
            destination.NextBlock = NextBlock;
            destination.Columns = Columns;
            destination.ColumnKeys = ColumnKeys;
            destination.Values = Values;
            destination.RowKeys = RowKeys;

            Lo = default;
            Hi = -1;
            RowCapacity = default;
            ColumnCount = default;
            Height = default;
            _refCount = default;
            PreviousBlock = default;
            NextBlock = default;
            Columns = default;
            ColumnKeys = default;
            Values = default;
            RowKeys = default;
        }

        [Conditional("DEBUG")]
        private void EnsureDisposed()
        {
            ThrowHelper.DebugAssert(AtomicCounter.GetIsDisposed(ref _refCount));
            ThrowHelper.DebugAssert(Lo == default);
            ThrowHelper.DebugAssert(Hi == -1);
            ThrowHelper.DebugAssert(RowCapacity == default);
            ThrowHelper.DebugAssert(RowCount == default);
            ThrowHelper.DebugAssert(ColumnCount == default);
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
                // ThrowHelper.ThrowInvalidOperationException("Cannot dispose DataBlock.Empty sentinel");
                return; // TODO review if we use it on a hot path to avoid checks. If not, need to throw. If yes, comment this here.

            var zeroIfDisposedNow = AtomicCounter.TryDispose(ref _refCount);

            if (zeroIfDisposedNow > 0)
                BuffersThrowHelper.ThrowDisposingRetained<DataBlock>();

            if (zeroIfDisposedNow == -1)
                ThrowHelper.ThrowObjectDisposedException(nameof(DataBlock));

            if (Height > 0)
                DisposeNode();

            Hi = -1;
            Lo = default;
            RowCapacity = default;
            ColumnCount = default;
            Height = default;

            if (NextBlock != null)
            {
                ThrowHelper.DebugAssert(NextBlock.PreviousBlock == this);
                NextBlock.PreviousBlock = null;
                NextBlock = null;
            }

            if (PreviousBlock != null)
            {
                ThrowHelper.DebugAssert(PreviousBlock.NextBlock == this);
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
                    Columns[i].Dispose();
                }

                Columns = default;
            }

            var pooled = ObjectPool.Return(this);

            // See comments in PrivateMemory.Free
            if (pooled && !disposing)
                GC.ReRegisterForFinalize(this);
            if (!pooled && disposing)
                GC.SuppressFinalize(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeNode()
        {
            // TODO review, we start at 0 not Lo. When Lo > 0 values could stay for a while in moving window case.
            for (int i = 0; i <= Hi; i++)
            {
                DataBlock? node = Values.UnsafeReadUnaligned<DataBlock?>(i);
                node?.Decrement();
            }
        }

        public void Dispose() => Dispose(true);

        ~DataBlock()
        {
#if DEBUG
            ThrowHelper.ThrowInvalidOperationException("Finalizing DataBlock");
#endif
            Dispose(false);
        }

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

        // TODO delete this method
        [Obsolete("Use container-specific factories, e.g. CreateForSeries")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DataBlock Create(RetainedVec rowIndex = default, RetainedVec values = default,
            RetainedVec[]? columns = null, int rowLength = -1)
        {
            var block = ObjectPool.Rent()!;
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
                block.Hi = rowCapacity - 1;
            }
            else
            {
                if ((uint) rowLength > rowCapacity)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("rowLength");
                }
                else
                {
                    block.Hi = rowLength - 1;
                }
            }

            block.Lo = 0;
            block._refCount = 0;

            ThrowHelper.DebugAssert(!block.IsDisposed, "!block.IsDisposed");

            return block;
        }
    }
}
