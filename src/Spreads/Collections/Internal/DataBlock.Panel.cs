// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Utils;

namespace Spreads.Collections.Internal
{
    internal sealed partial class DataBlock
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeGetRowKeyValue<TKey, TValue>(int index, out TKey key, out TValue value)
        {
            ThrowHelper.DebugAssert((uint)index < RowCount, "UnsafeGetRowKeyValue: (uint)index < RowCount");
            var offset = Lo + index;

            if (typeof(TKey) == typeof(Index))
                // ReSharper disable once HeapView.BoxingAllocation
                key = (TKey)(object)new Index(index);
            else
                key = RowKeys.UnsafeReadUnaligned<TKey>(offset);

            value = Values.UnsafeReadUnaligned<TValue>(offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnsafeGetRowKey<T>(int index)
        {
            if(typeof(T) == typeof(Index))
                // ReSharper disable once HeapView.BoxingAllocation
                return (T)(object)new Index(index);
            return RowKeys.UnsafeReadUnaligned<T>(Lo + index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnsafeGetValue<T>(int index)
        {
            return Values.UnsafeReadUnaligned<T>(Lo + index);
        }

        [Conditional("DEBUG")]
        private void EnsurePanelLayout()
        {
            // TODO more checks
            if (Columns != null)
                ThrowHelper.ThrowInvalidOperationException("_columns != null in panel layout");

            ThrowHelper.DebugAssert(ColumnKeys == default && ColumnCount == 1 || ColumnCount == ColumnKeys.Length);

            var valueCount = RowKeys.Length * ColumnCount;

            ThrowHelper.Assert(valueCount <= Values.Length, "Values vector must have enough capacity for data");
        }

        /// <summary>
        /// Create a DataBlock for Series or Panel.
        /// </summary>
        public static DataBlock CreateForPanel(RetainedVec rowKeys = default, RetainedVec values = default, int rowCount = -1, RetainedVec columnKeys = default,
            int columnCount = -1)
        {
            // this must work with rowLength == 0 and empty vecs, this is how we initialize empty block that is not sentinel

            if (columnCount < 1)
            {
                columnCount = Math.Max(1, columnKeys.Length);
            }
            else
            {
                if ((uint) columnCount > columnKeys.Length)
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(columnCount));
            }

            // RetainedVec is slice-able, easy to get exact layout
            ThrowHelper.Assert(rowKeys.Length == values.Length * columnCount, "rowKeys.Length != values.Length * columnCount");

            var block = ObjectPool.Rent()!;
            block.EnsureDisposed();

            var rowCapacity = rowKeys.Length;

            block.RowKeys = rowKeys;
            block.ColumnKeys = columnKeys;
            block.Values = values;

            if (rowCount == -1)
            {
                block.Hi = rowCapacity - 1;
            }
            else
            {
                if ((uint) rowCount > rowCapacity)
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(rowCount));
                else
                    block.Hi = rowCount - 1;
            }

            block.ColumnCount = columnCount;

            block.RowCapacity = rowCapacity;
            block.Lo = 0;

            block._refCount = 0;

            block.EnsurePanelLayout();

            ThrowHelper.DebugAssert(!block.IsDisposed, "!block.IsDisposed");

            return block;
        }

        public static DataBlock CreateForVector<TValue>(int rowCapacity = -1)
        {
            return CreateForSeries<Index, TValue>(rowCapacity);
        }

        public static DataBlock CreateSeries<TRowKey, TValue>()
        {
            var block = CreateForSeries<TRowKey, TValue>(rowCapacity: -1);
            return block;
        }

        public static DataBlock CreateForSeries<TRowKey, TValue>(int rowCapacity = -1)
        {
            return CreateForPanel<TRowKey, Index, TValue>(rowCapacity, columnCount: -1);
        }

        public static DataBlock CreateForPanel<TRowKey, TColumnKey, TValue>(int rowCapacity = -1,
            int columnCount = -1)
        {
            RetainedVec columnKeys = default;
            if (typeof(TColumnKey) != typeof(Index))
            {
                var columnKeysMemory = BufferPool<TRowKey>.MemoryPool.RentMemory(columnCount, exactBucket: true);
                columnKeys = RetainedVec.Create(columnKeysMemory, start: 0, columnKeysMemory.Length);
                ThrowHelper.Assert(columnCount >= 1);
            }

            var block = CreateForPanel(rowKeys: default, values: default, rowCount: -1, columnKeys, columnCount);
            block.IncreaseRowsCapacity<TRowKey, TValue>(rowCapacity);
            return block;
        }

        /// <summary>
        /// Returns new <see cref="RowCapacity"/> or -1 on failure
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal int IncreaseRowsCapacity<TRowKey, TValue>(int newCapacity = -1)
        {
            if (Lo != 0)
                ThrowHelper.ThrowNotImplementedException("Lo != 0"); // TODO

            EnsureNotSentinel();
            EnsurePanelLayout();

            if (RowKeys.RuntimeTypeId != 0)
            {
                if (RowKeys.RuntimeTypeId != TypeHelper<TRowKey>.RuntimeTypeId)
                    throw new ArrayTypeMismatchException(
                        $"Type of TRowKey {typeof(TRowKey)} doesn't match existing row keys type {TypeHelper.GetRuntimeTypeInfo(RowKeys.RuntimeTypeId).Type}");
            }
            else if (Hi >= 0)
            {
#pragma warning disable 618
                ThrowHelper.FailFast("Runtime type id for non-empty RowKeys was not set.");
#pragma warning restore 618
            }

            if (Values.RuntimeTypeId != 0)
            {
                if (Values.RuntimeTypeId != TypeHelper<TValue>.RuntimeTypeId)
                    throw new ArrayTypeMismatchException($"Type of TValue {typeof(TValue)} doesn't match existing value type {TypeHelper.GetRuntimeTypeInfo(Values.RuntimeTypeId).Type}");
            }
            else if (Hi >= 0)
            {
#pragma warning disable 618
                ThrowHelper.FailFast("Runtime type id for non-empty Values was not set.");
#pragma warning restore 618
            }

            var rowKeys = RowKeys;
            var values = Values;

            var minCapacity = Math.Max(newCapacity, Settings.MIN_POOLED_BUFFER_LEN);
            var newRowCapacity = BitUtils.NextPow2(Math.Max(minCapacity, RowCapacity + 1));

            RetainableMemory<TRowKey>? newRowKeysBuffer = null;
            RetainedVec newRowKeys = default;
            RetainableMemory<TValue>? newValuesBuffer = null;
            RetainedVec newValues = default;

            try
            {
                if (typeof(TRowKey) != typeof(Index))
                {
                    newRowKeysBuffer = BufferPool<TRowKey>.MemoryPool.RentMemory(newRowCapacity, exactBucket: true);
                    ThrowHelper.DebugAssert(newRowKeysBuffer.Length == newRowCapacity);
                    newRowKeys = RetainedVec.Create(newRowKeysBuffer, start: 0, newRowKeysBuffer.Length); // new buffer could be larger
                }

                newValuesBuffer = BufferPool<TValue>.MemoryPool.RentMemory(newRowCapacity * ColumnCount, exactBucket: true);
                ThrowHelper.DebugAssert(newValuesBuffer.Length == newRowCapacity * ColumnCount);
                newValues = RetainedVec.Create(newValuesBuffer, start: 0, newValuesBuffer.Length);

                if (Hi >= 0)
                {
                    // TODO Lo != 0 throws above, need to decide from where to copy: 0 or Lo
                    // Depends on what cursors store - index or offset. Index will require comparison
                    // with RowCount, which is 2 volatile reads and 2 add ops, so it looks like we
                    // want to copy from zero and store block offset from zero in cursors.
                    // TODO but should be copy the part before Lo? Should increase capacity be completely transparent
                    // without side effects other than - as in the name - capacity? It looks like this is the case, but need to review.
                    if (typeof(TRowKey) != typeof(Index))
                        rowKeys.GetSpan<TRowKey>().Slice(start: 0, Hi + 1).CopyTo(newRowKeys.GetSpan<TRowKey>());
                    values.GetSpan<TValue>().Slice(start: 0, (Hi + 1) * ColumnCount).CopyTo(newValues.GetSpan<TValue>());
                }
            }
            catch (OutOfMemoryException)
            {
                if (typeof(TRowKey) != typeof(Index))
                {
                    if (newRowKeys != default)
                        newRowKeys.Dispose();
                    else
                        newRowKeysBuffer?.DecrementIfOne();
                }

                if (newValues != default)
                    newValues.Dispose();
                else
                    newValuesBuffer?.DecrementIfOne();

                // TODO log this event
                return -1;
            }

            try
            {
                try
                {
                }
                finally
                {
                    // we have all needed buffers, must switch in one operation

                    if (typeof(TRowKey) != typeof(Index))
                    {
                        RowKeys = newRowKeys;
                        rowKeys.Dispose();
                    }

                    Values = newValues;
                    values.Dispose();

                    RowCapacity = newRowCapacity;
                }

                return RowCapacity;
            }
            catch
            {
                return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal void AppendToBlock<TKey, TValue>(TKey key, TValue value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                EnsurePanelLayout();
                EnsureNotSentinel();
                ThrowHelper.Assert(RowCount < RowKeys.Length, "RowCount < RowKeys.Length");
                ThrowHelper.Assert(RowKeys.RuntimeTypeId == TypeHelper<TKey>.RuntimeTypeId, "RowKeys.RuntimeTypeId == VecTypeHelper<TKey>.RuntimeTypeId");
                ThrowHelper.Assert(Values.RuntimeTypeId == TypeHelper<TValue>.RuntimeTypeId, "Values.RuntimeTypeId == VecTypeHelper<TValue>.RuntimeTypeId");
            }

            var offset = Hi + 1;

            RowKeys.UnsafeWriteUnaligned(offset, key);
            Values.UnsafeWriteUnaligned(offset, value);

            // volatile increment goes last
            Hi = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal void AppendToBlock<TKey, TValue>(TKey key, ReadOnlySpan<TValue> values)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                EnsurePanelLayout(); // this guaranties that values have capacity for _rowKeys.Length * _columnCount
                EnsureNotSentinel();
                ThrowHelper.Assert(RowCount < RowKeys.Length, "RowCount < RowKeys.Length");
                ThrowHelper.Assert(RowKeys.RuntimeTypeId == TypeHelper<TKey>.RuntimeTypeId, "RowKeys._runtimeTypeId == VecTypeHelper<TKey>.RuntimeTypeId");
                ThrowHelper.Assert(Values.RuntimeTypeId == TypeHelper<TValue>.RuntimeTypeId, "Values._runtimeTypeId == VecTypeHelper<TValue>.RuntimeTypeId");
                ThrowHelper.Assert(values.Length == ColumnCount);
            }

            var offset = Hi + 1;

            RowKeys.UnsafeWriteUnaligned(offset, key);
            Values.UnsafeWriteUnaligned(offset, values);

            // volatile increment goes last
            Hi = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool TryAppendToBlock<TKey, TValue>(TKey key, TValue value, bool increaseCapacity)
        {
            if (Hi + 1 >= RowCapacity)
            {
                if (!IncreaseCapacity())
                    return false;
            }

            AppendToBlock(key, value);
            return true;

            bool IncreaseCapacity()
            {
                if (!increaseCapacity)
                    return false;

                var newCapacity = RowCapacity * 2;

                if (newCapacity > MaxNodeSize)
                    return false;

                var actualCapacity = IncreaseRowsCapacity<TKey, TValue>(newCapacity);
                ThrowHelper.Assert(Hi + 1 < actualCapacity); //
                return true;
            }
        }
    }
}
