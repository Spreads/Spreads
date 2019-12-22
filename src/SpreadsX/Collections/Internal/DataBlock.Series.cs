// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    // TODO Review & test Series methods after refactoring up to the new Series implementation.
    // Replacing SortedMap with new Series in tests should catch most errors, but some edge cases should
    // be explicitly tested, e.g. OOM in SeriesIncreaseCapacity using ChaosMonkey.

    internal sealed partial class DataBlock
    {
        [Conditional("DEBUG")]
        private void EnsureSeriesLayout()
        {
            // TODO more checks
            if (_columns != null || _rowKeys.Vec.Length != _values.Vec.Length)
            {
                throw new DataBlockLayoutException("Bad Series layout");
            }
        }

        /// <summary>
        /// Create a DataBlock for Series.
        /// </summary>
        public static DataBlock SeriesCreate(VecStorage rowIndex = default, VecStorage values = default, int rowLength = -1)
        {
            if (rowIndex.Vec.Length < 0 || values.Vec.Length < 0 || rowIndex.Vec.Length != values.Vec.Length)
            {
                ThrowHelper.ThrowArgumentException($"rowIndex.Length [{rowIndex.Vec.Length}] <= 0 || values.Length [{values.Vec.Length}]  <= 0 || rowIndex.Length != values.Length");
            }

            var block = ObjectPool.Allocate();
            block.EnsureDisposed();

            var rowCapacity = rowIndex.Vec.Length;

            block._rowKeys = rowIndex;

            block._values = values;

            if (rowLength == -1)
            {
                block._rowCount = rowCapacity;
            }
            else
            {
                if ((uint)rowLength > rowCapacity)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("rowLength");
                }
                else
                {
                    block._rowCount = rowLength;
                }
            }
            block._head = 0;
            block.EnsureSeriesLayout();

            block._refCount = 0;

            ThrowHelper.DebugAssert(!block.IsDisposed, "!block.IsDisposed");

            return block;
        }

        /// <summary>
        /// Insert key to RowIndex and value in Values only if there is enough capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal void SeriesAppend<TKey, TValue>(int index, TKey key, TValue value)
        {
            EnsureSeriesLayout();

            if (AdditionalCorrectnessChecks.Enabled)
            {
                SeriesAdditionalInsertChecks(index);
            }

            Debug.Assert(index == RowCount);

            _rowKeys.Vec.DangerousGetRef<TKey>(index) = key;
            _values.Vec.DangerousGetRef<TValue>(index) = value;

            // volatile increment goes last
            _rowCount++;
        }

        /// <summary>
        /// Insert key to RowIndex and value in Values only if there is enough capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal void SeriesInsert<TKey, TValue>(int index, TKey key, TValue value)
        {
            EnsureSeriesLayout();

            if (AdditionalCorrectnessChecks.Enabled)
            {
                SeriesAdditionalInsertChecks(index);
            }

            if (index < RowCount) // TODO Extract AddLast case. It has only this cost, but the method size could be an issue. We could keep normal inserts as non-inlined, but AddLast must be very fast and it does little checks & work.
            {
                var len = RowCount - index;
                var rowKeysSpan = _rowKeys.Vec.AsSpan<TKey>();
                rowKeysSpan.Slice(index, len).CopyTo(rowKeysSpan.Slice(index + 1, len));
                var valuesSpan = _values.Vec.AsSpan<TValue>();
                valuesSpan.Slice(index, len).CopyTo(valuesSpan.Slice(index + 1, len));
            }

            _rowKeys.Vec.DangerousGetRef<TKey>(index) = key;
            _values.Vec.DangerousGetRef<TValue>(index) = value;

            // volatile increment goes last
            _rowCount++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SeriesAdditionalInsertChecks(int index)
        {
            EnsureNotSentinel();

            if ((uint)index > RowCount)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("index");
            }

            if (RowCount == _rowKeys.Vec.Length)
            {
                ThrowHelper.ThrowInvalidOperationException("Not enough capacity");
            }

            if (RowCount > _rowKeys.Vec.Length)
            {
                ThrowHelper.FailFast("Series DataBlock.RowLength exceeded capacity. That should never happen.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal int SeriesIncreaseCapacity<TKey, TValue>(int newCapacity = -1)
        {
            EnsureSeriesLayout();
            EnsureNotSentinel();

            // TODO At RMP level we must make sure that memory is never allocated
            // larger than requested at least when the next buffer is in LOH
            // all LOH buffers are 128k to avoid LOH fragmentation

            // TODO handle OutOfMemory in RentMemory, operation must be atomic in a sense that any error does not change existing data, no partial updates.

            // TODO _rowIndex.Vec.Length could be already 2x larger because array pool could have returned larger array on previous doubling
            // TODO (!, new) VS now hides total capacity of RM, we could get RM by casting and here we have types.
            // But is it always true that unused part of RM is always free and we could just expand to it without copying?

            // We ignore this now
            //if (_rowIndex.Vec.Length != _rowIndex.Length)
            //{
            //    Console.WriteLine($"_rowIndex.Vec.Length {_rowIndex.Vec.Length} != _rowIndex.Length {_rowIndex.Length}");
            //}

            var ri = _rowKeys;
            var vals = _values;

            var minCapacity = Math.Max(newCapacity, Settings.MIN_POOLED_BUFFER_LEN);
            var newLen = Math.Max(minCapacity, BitUtil.FindNextPositivePowerOfTwo(ri.Vec.Length + 1));

            RetainableMemory<TKey>? newRiBuffer = null;
            VecStorage newRi = default;
            RetainableMemory<TValue>? newValsBuffer = null;
            VecStorage newVals = default;

            try
            {
                newRiBuffer = BufferPool<TKey>.MemoryPool.RentMemory(newLen);

                newRi = VecStorage.Create(newRiBuffer, 0, newRiBuffer.Length); // new buffer could be larger
                if (ri.Vec.Length > 0)
                {
                    ri.Vec.AsSpan<TKey>().CopyTo(newRi.Vec.AsSpan<TKey>());
                }

                newValsBuffer = BufferPool<TValue>.MemoryPool.RentMemory(newLen);

                newVals = VecStorage.Create(newValsBuffer, 0, newValsBuffer.Length);
                if (vals.Vec.Length > 0)
                {
                    vals.Vec.AsSpan<TValue>().CopyTo(newVals.Vec.AsSpan<TValue>());
                }
            }
            catch (OutOfMemoryException)
            {
                if (newRi != default)
                {
                    newRi.Dispose();
                }
                else
                {
                    newRiBuffer?.DecrementIfOne();
                }

                if (newVals != default)
                {
                    newVals.Dispose();
                }
                else
                {
                    newValsBuffer?.DecrementIfOne();
                }

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

                    _rowKeys = newRi;
                    _values = newVals;

                    ri.Dispose();
                    vals.Dispose();
                }

                return _rowKeys.Vec.Length;
            }
            catch
            {
                return -1;
            }
        }


        internal bool SeriesTrimFirstValue<TKey, TValue>(out TKey key, out TValue value)
        {
            throw new NotImplementedException();
            //value = DangerousValueRef<TValue>(0);

            //return _rowCount > 0;

            
        }
    }
}
