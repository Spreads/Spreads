using Spreads.Collections.Concurrent;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    /// <summary>
    /// Physycal storage for Series, Matrix and DataFrame chunks.
    /// </summary>
    internal class DataChunkStorage : IDisposable, IData
    {
        private static readonly ObjectPool<DataChunkStorage> ObjectPool = new ObjectPool<DataChunkStorage>(() => new DataChunkStorage(), Environment.ProcessorCount * 16);

        // for structural sharing no references to this should be exposed outside, only new object (or from pool)

        /// <summary>
        /// Length of existing data vs storage capacity. Owners set this value and determine capacity from vector storages.
        /// </summary>
        internal int RowLength;

        internal int RowCapacity;

        // TODO these must be lazy! 
        internal VectorStorage _rowIndex;

        internal VectorStorage _values;

        internal VectorStorage _columnIndex;

        internal VectorStorage[] _columns; // arrays is allocated and GCed, so far OK

        // TODO could automatically keep a chain of chunks that otherwise only weakly referenced
        private DataChunkStorage _nextChunk;

        private Mutability _mutability;

        [MethodImpl(MethodImplOptions.NoInlining)] // TODO remove, testing
        internal DataChunkStorage TryGetNextChunk()
        {
            return _nextChunk;
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

        public Mutability Mutability
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _mutability;
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
                        Debug.Assert(vectorStorage._vec._runtimeTypeId == _values._vec._runtimeTypeId);
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
            get => _values == null && _columns == null;
        }

        private void Dispose(bool disposing)
        {
            if (_columns != null)
            {
                foreach (var vectorStorage in _columns)
                {
                    // shared columns just do not have proper handle to unpin and their call is void
                    vectorStorage.Dispose();
                }
                _columns = null;
            }

            if (_values != null)
            {
                _values.Dispose();
                _values = null;
            }

            if (_rowIndex != null)
            {
                _rowIndex.Dispose();
                _rowIndex = null;
            }

            if (_columnIndex != null)
            {
                _columnIndex.Dispose();
                _columnIndex = null;
            }

            // just break the chain, if the remaining linked list was only rooted here it will be GCed TODO review
            _nextChunk = null;

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

        ~DataChunkStorage()
        {
            Dispose(false);
        }

        #endregion Dispose logic
    }

    internal class DataChunkSource<TKey> : ISeries<TKey, DataChunkStorage>
    {
        // TODO
        /// <summary>
        ///  For append-only containers chunks will have the same size. Last chunk could be only partially filled.
        /// </summary>
        public uint ConstantChunkLength = 0;

        public IAsyncEnumerator<KeyValuePair<TKey, DataChunkStorage>> GetAsyncEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, DataChunkStorage>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool IsCompleted => throw new NotImplementedException();

        public bool IsIndexed => throw new NotImplementedException();

        public ICursor<TKey, DataChunkStorage> GetCursor()
        {
            throw new NotImplementedException();
        }

        public KeyComparer<TKey> Comparer => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, DataChunkStorage>> First => throw new NotImplementedException();

        public Opt<KeyValuePair<TKey, DataChunkStorage>> Last => throw new NotImplementedException();

        public DataChunkStorage this[TKey key] => throw new NotImplementedException();

        public bool TryGetValue(TKey key, out DataChunkStorage value)
        {
            throw new NotImplementedException();
        }

        public bool TryGetAt(long index, out KeyValuePair<TKey, DataChunkStorage> kvp)
        {
            throw new NotImplementedException();
        }

        public bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, DataChunkStorage> kvp)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TKey> Keys => throw new NotImplementedException();

        public IEnumerable<DataChunkStorage> Values => throw new NotImplementedException();
    }
}