// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using Spreads.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    internal interface INextBlockGetter
    {
        DataBlock GetNextBlock(DataBlock current);
    }

    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame blocks.
    /// </summary>
    internal sealed class DataBlock : IDisposable
    {
        // We need it as a sentinel with RowLength == 0 in cursor to not penalize fast path of single-block containers
        internal static readonly DataBlock Empty = new DataBlock();

        private static readonly ObjectPool<DataBlock> ObjectPool = new ObjectPool<DataBlock>(() => new DataBlock(), Environment.ProcessorCount * 16);

        private DataBlock()
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DataBlock Create(VectorStorage rowIndex = null, VectorStorage columnIndex = null, VectorStorage values = null, VectorStorage[] columns = null, int rowLength = -1)
        {
            var block = ObjectPool.Allocate();

            Debug.Assert(block._rowLength == -1);
            Debug.Assert(block._values == VectorStorage.Empty);
            Debug.Assert(block._rowIndex == VectorStorage.Empty);
            Debug.Assert(block._columnIndex == VectorStorage.Empty);
            Debug.Assert(block._columns == null);

            var maxRowLen = 0;

            if (rowIndex != null)
            {
                block._rowIndex = rowIndex;
                maxRowLen = rowIndex.Length;
            }

            if (columnIndex != null)
            {
                block._columnIndex = columnIndex;
            }

            if (values != null)
            {
                block._values = values;
                maxRowLen = Math.Min(maxRowLen, values.Length);
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
                    maxRowLen = Math.Min(maxRowLen, column.Length);
                }
            }

            if (rowLength == -1)
            {
                block._rowLength = maxRowLen;
            }
            else
            {
                if ((uint)rowLength > maxRowLen)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException("rowLength");
                }
                else
                {
                    block._rowLength = rowLength;
                }
            }

            return block;
        }

        // for structural sharing no references to this should be exposed outside, only new object (or from pool)

        /// <summary>
        /// Length of existing data vs storage capacity. Owners set this value and determine capacity from vector storages.
        /// </summary>
        internal int _rowLength = -1;

        // it's calculated from context, for Matrix is different than from vec.
        // internal int RowCapacity;

        // TODO these should be lazy?
        private VectorStorage _rowIndex = VectorStorage.Empty;

        private VectorStorage _columnIndex = VectorStorage.Empty;

        private VectorStorage _values = VectorStorage.Empty;

        private VectorStorage[] _columns; // arrays is allocated and GCed, so far OK

        internal DataBlock NextBlock;

        // TODO review: this could allow a double linked list that will automatically shrink to the closest used block.
        // internal WeakReference<DataBlock> PreviousBlock;

        // TODO interface with a single impl for persistence so that it's methods are devirtualized
        // internal bool HasTryGetNextBlockImpl;

        /// <summary>
        /// Fast path to get the next block from the current one.
        /// (TODO review) callers should not rely on this return value:
        /// if null it does NOT mean that there is no next block but
        /// it just means that we cannot get it in a super fast way (whatever
        /// this means depends on implementation).
        /// </summary>
        /// <remarks>
        /// This method should not be virtual, rather a flag should be used to call
        /// a virtual implementation if it guarantees faster lookup than <see cref="ISeries{TKey,TValue}.TryFindAt"/>
        /// even with the virtual call overhead. E.g. we could have O(1) vs O(log n)
        /// or significantly save on some constant.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DataBlock TryGetNextBlock()
        {
            if (NextBlock != null)
            {
                // In-memory implementation just externally manages this field without TryGetNextBlockImpl
                return NextBlock;
            }
            // TODO we cannot have virtual methods in a pooled object
            //if (HasTryGetNextBlockImpl)
            //{
            //    return TryGetNextBlockImpl();
            //}
            return null;
        }

        //// TODO JIT could devirt this if there is only one implementation, but we have two
        //// Check under what conditions this method in a derived sealed class is devirtualized.
        //public virtual DataBlock TryGetNextBlockImpl()
        //{
        //    return null;
        //}

        public int RowLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowLength;
        }

        public VectorStorage RowIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_rowIndex == null)
                {
                    // TODO lazy load if lazy is supported
                }
                return _rowIndex;
            }
        }

        public VectorStorage Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_rowIndex == null)
                {
                    // TODO lazy load if lazy is supported
                }
                return _values;
            }
        }

        /// <summary>
        /// Insert key to RowIndex and value in Values only if there is enought capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal void InsertSeries<TKey, TValue>(int index, TKey key, TValue value)
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                DoAdditoinalInsertChecks(index);
            }

            if (index < RowLength)
            {
                var len = RowLength - index;
                var rsp = _rowIndex.Vec.AsSpan<TKey>();
                rsp.Slice(index, len).CopyTo(rsp.Slice(index + 1, len));
                var vsp = _values.Vec.AsSpan<TValue>();
                vsp.Slice(index, len).CopyTo(vsp.Slice(index + 1, len));
            }
            _rowIndex.Vec.DangerousGetRef<TKey>(index) = key;
            _values.Vec.DangerousGetRef<TValue>(index) = value;
            _rowLength++;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DoAdditoinalInsertChecks(int index)
        {
            EnsureNotSentinel();

            if ((uint)index > RowLength)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("index");
            }

            if (RowLength == _rowIndex.Length)
            {
                ThrowHelper.ThrowInvalidOperationException("Not enough capacity");
            }

            if (RowLength > _rowIndex.Length || _rowIndex.Length != _values.Length)
            {
                ThrowHelper.FailFast("Bad layout of Series DataBlock");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal int IncreaseSeriesCapacity<TKey, TValue>(int newCapacity = -1)
        {
            EnsureNotSentinel();

            // TODO handle OutOfMemory in RentMemory, operation must be atomic in a sense that any error does not change existing data

            // TODO _rowIndex.Vec.Length could be already 2x larger because array pool could have returned larger array on previous doubling
            // We ignore this now
            //if (_rowIndex.Vec.Length != _rowIndex.Length)
            //{
            //    Console.WriteLine($"_rowIndex.Vec.Length {_rowIndex.Vec.Length} != _rowIndex.Length {_rowIndex.Length}");
            //}

            var ri = _rowIndex;
            var vals = _values;

            var minCapacity = Math.Max(newCapacity, Settings.MIN_POOLED_BUFFER_LEN);
            var newLen = Math.Max(minCapacity, BitUtil.FindNextPositivePowerOfTwo(ri.Length + 1));

            RetainableMemory<TKey> newRiBuffer = null;
            VectorStorage newRi = null;
            RetainableMemory<TValue> newValsBuffer = null;
            VectorStorage newVals = null;

            try
            {
                newRiBuffer = BufferPool<TKey>.MemoryPool.RentMemory(newLen);

                newRi = VectorStorage.Create(newRiBuffer, 0, newRiBuffer.Length,
                    elementLength: newLen); // new buffer could be larger
                if (ri.Length > 0)
                {
                    ri.Vec.AsSpan<TKey>().CopyTo(newRi.Vec.AsSpan<TKey>());
                }

                newValsBuffer = BufferPool<TValue>.MemoryPool.RentMemory(newLen);

                newVals = VectorStorage.Create(newValsBuffer, 0, newValsBuffer.Length, elementLength: newLen);
                if (vals.Length > 0)
                {
                    vals.Vec.AsSpan<TValue>().CopyTo(newVals.Vec.AsSpan<TValue>());
                }
            }
            catch (OutOfMemoryException)
            {
                if (newRi != null)
                {
                    newRi.Dispose();
                }
                else
                {
                    newRiBuffer?.DecrementIfOne();
                }

                if (newVals != null)
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

                    _rowIndex = newRi;
                    _values = newVals;

                    ri.Dispose();
                    vals.Dispose();
                }

                return _rowIndex.Length;
            }
            catch
            {
                return -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotSentinel()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (ReferenceEquals(this, Empty))
                {
                    DoThrow();
                }
                void DoThrow()
                {
                    ThrowHelper.ThrowInvalidOperationException("DataBlock.Empty must only be used as sentinel");
                }
            }
        }

        #region Structure check

        // TODO do not pretend to make it right at the first take, will review and redefine
        // TODO make tests
        public bool IsAnyColumnShared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_values == null)
                {
                    return false;
                }

                if (_columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memorySource == _values._memorySource)
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
                if (_values == null)
                {
                    return false;
                }

                if (_columns == null)
                {
                    return false;
                }

                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memorySource != _values._memorySource)
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
                if (colLength >= 0 && _values != null && !_columns.Any(c => c._memorySource == _values._memorySource))
                {
                    // have _value set without shared source, that is not supported
                    return false;
                }

                if (colLength == -1 && _values != null)
                {
                    colLength = _values.Length;
                }

                if (_rowIndex != null && _rowIndex.Length != colLength)
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
                if (_values == null)
                {
                    return false;
                }
                if (!(_columns != null && _columns.Length > 0))
                {
                    return false;
                }

                var len = -1;
                foreach (var vectorStorage in _columns)
                {
                    if (vectorStorage._memorySource != _values._memorySource)
                    {
                        return false;
                    }
                    else
                    {
                        Debug.Assert(vectorStorage.Vec._runtimeTypeId == _values.Vec._runtimeTypeId);
                    }
                }

                return true;
            }
        }

        #endregion Structure check

        #region Dispose logic

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RowLength < 0;
        }

        private void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                ThrowHelper.ThrowObjectDisposedException("DataBlock");
            }
            _rowLength = -1;

            // just break the chain, if the remaining linked list was only rooted here it will be GCed TODO review
            NextBlock = null;

            if (_columns != null)
            {
                foreach (var vectorStorage in _columns)
                {
                    // shared columns just do not have proper handle to unpin and their call is void
                    vectorStorage.Dispose();
                }
                _columns = null;
            }

            if (_rowIndex != VectorStorage.Empty)
            {
                _rowIndex.Dispose();
                _rowIndex = VectorStorage.Empty;
            }

            if (_columnIndex != VectorStorage.Empty)
            {
                _columnIndex.Dispose();
                _columnIndex = VectorStorage.Empty;
            }

            if (_values != VectorStorage.Empty)
            {
                _values.Dispose();
                _values = VectorStorage.Empty;
            }

            ObjectPool.Free(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WarnFinalizing()
        {
            Trace.TraceWarning("Finalizing VectorStorage. It must be properly disposed.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException("source");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataBlock()
        {
            ThrowHelper.ThrowInvalidOperationException("Finalizing DataBlock");
            Dispose(false);
        }

        #endregion Dispose logic
    }
}