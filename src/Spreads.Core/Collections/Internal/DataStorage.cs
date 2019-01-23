using Spreads.Collections.Concurrent;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Collections.Internal
{
    /// <summary>
    /// Storage for Series, Matrix and DataFrame
    /// </summary>
    internal class DataStorage : IDisposable
    {
        private static readonly ObjectPool<DataStorage> ObjectPool = new ObjectPool<DataStorage>(() => new DataStorage(), Environment.ProcessorCount * 16);

        // for structural sharing no references to this should be exposed outside, only new object (or from pool)

        private VectorStorage _rowIndex;
        private VectorStorage _values;

        private VectorStorage _columnIndex;
        
        private VectorStorage[] _columns; // arrays is allocated and GCed, so far OK

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

        ~DataStorage()
        {
            Dispose(false);
        }

        #endregion Dispose logic
    }
}