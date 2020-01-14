// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Spreads
{
    public class AppendSeries<TKey, TValue> : Series<TKey, TValue>, IAppendSeries<TKey, TValue>
    {
        private static readonly int DefaultMaxBlockRowCount = Math.Max(Settings.MIN_POOLED_BUFFER_LEN, Settings.LARGE_BUFFER_LIMIT / Math.Max(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>()));

        /// <summary>
        /// Window options.
        /// </summary>
        internal MovingWindowOptions? WindowOptions;

        #region Public ctors

        public AppendSeries() :
            this(Mutability.AppendOnly)
        {
        }

        public AppendSeries(KeyComparer<TKey> comparer)
            : this(Mutability.AppendOnly, KeySorting.Strong, comparer)
        {
        }

        public AppendSeries(KeySorting keySorting)
            : this(Mutability.AppendOnly, keySorting)
        {
        }

        public AppendSeries(KeyComparer<TKey> comparer, KeySorting keySorting)
            : this(Mutability.AppendOnly, keySorting, comparer, default)
        {
        }

        public AppendSeries(MovingWindowOptions<TKey> movingWindowOptions)
            : this(Mutability.AppendOnly, KeySorting.Strong, default, movingWindowOptions)
        {
        }

        public AppendSeries(KeySorting keySorting, MovingWindowOptions<TKey> movingWindowOptions)
            : this(Mutability.AppendOnly, keySorting, default, movingWindowOptions)
        {
        }

        public AppendSeries(KeyComparer<TKey> comparer, KeySorting keySorting, MovingWindowOptions<TKey> movingWindowOptions)
            : this(Mutability.AppendOnly, keySorting, comparer, movingWindowOptions)
        {
        }

        #endregion Public ctors

        protected AppendSeries(Mutability mutability = Mutability.ReadOnly,
            KeySorting keySorting = KeySorting.Strong,
            KeyComparer<TKey> comparer = default,
            MovingWindowOptions<TKey>? movingWindowOptions = default) : base(mutability, keySorting, comparer)
        {
            if (movingWindowOptions != null)
                WindowOptions = new MovingWindowOptions(this, movingWindowOptions);
        }

        /// <summary>
        /// Thread-unsafe equivalent of <see cref="TryAppend"/>.
        /// 2x faster but concurrent access could corrupt data or
        /// readers could get wrong results.
        /// Use it only when single-threaded access is guaranteed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public bool DangerousTryAppend(TKey key, TValue value)
        {
            if (Mutability == Mutability.ReadOnly)
                return false;

            if (!IsDataBlock(out var db, out var ds))
                db = ds.LastValueOrDefault!;

#if BUILTIN_NULLABLE
            Debug.Assert(db != null, "Data source must not be set if empty");
#endif
            var dbRowCount = db.RowCount;
            if (dbRowCount > 0)
            {
                var lastKey = db.DangerousRowKey<TKey>(dbRowCount - 1);

                var c = _comparer.Compare(key, lastKey);
                if (c <= 0 // faster path is c > 0
                    && (c < 0 & KeySorting == KeySorting.Weak)
                    | // no short-circuit && or || here
                    (c == 0 & KeySorting == KeySorting.Strong)
                    )
                {
                    // TODO detect which condition caused that in Append.ThrowCannotAppend
                    return false;
                }
            }

            object? data = null;
            if (dbRowCount == db.RowCapacity)
            {
                if (!TryAppendGrowCapacity(key, value, ref db, out data))
                {
                    return false;
                }
                // increased capacity and added values
            }
            else
            {
                // WindowOptions?.OnBeforeAppend();
                db.SeriesAppend(key, value);
            }

            // Switch Data only after adding values to a data block.
            // Otherwise DS could have an empty block for a short
            // time and that requires a lot of special case handling
            // on readers' side.
            if (data != null)
                Data = data;

            // TODO unchecked { Version++; }

            NotifyUpdate();

            return true;
        }

        internal int MaxBlockRowCount
        {
            get
            {
                //if (WindowOptions?.Options != null
                //    && WindowOptions.Options is MovingWindowOptions<TKey, TValue> typedMvo
                //    && typedMvo.WindowBlockSize > 0)
                //{
                //    return typedMvo.WindowBlockSize;
                //}
                return DefaultMaxBlockRowCount;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                     | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private bool TryAppendGrowCapacity(TKey key, TValue value, [NotNullWhen(true)] ref DataBlock? block, out object? data)
        {
            ThrowHelper.DebugAssert(block != null);
            data = null;
            try
            {
                if (block == DataBlock.Empty)
                {
                    block = DataBlock.SeriesCreate(rowLength: 0);
                    data = block;
                }

                // TODO review: do we want buffers in LOH or not? <= vs <
                // next increment will be 64kb, avoid buffer in LOH

                // ReSharper disable once PossibleNullReferenceException
                if (block.RowCapacity < MaxBlockRowCount)
                {
                    if (block.SeriesIncreaseCapacity<TKey, TValue>() < 0)
                    {
                        block = null;
                        return false;
                    }
                    // WindowOptions?.OnBeforeAppend();
                    block.SeriesAppend(key, value);
                }
                else
                {
                    // refactor switching to source logic to reuse in MutableSeries
                    if (IsDataBlock(out var db, out var ds))
                    {
                        Debug.Assert(ReferenceEquals(block, db));

                        ds = new DataBlockSource<TKey>();
                        ds.AddLast(block.DangerousRowKey<TKey>(0), block);
                        data = ds;
                    }

                    // before creating a new block try to remove first blocks that
                    // are not used and satisfy MovingWindowOptions
                    //if (!IsDataBlock(out _, out _)) 
                    //    WindowOptions?.OnBeforeNewBlock();

                    var minCapacity = block.RowCapacity;
                    var newBlock = DataBlock.SeriesCreate(rowLength: 0);
                    if (newBlock.SeriesIncreaseCapacity<TKey, TValue>(minCapacity) < 0)
                    {
                        block = null;
                        return false;
                    }

                    // WindowOptions?.OnBeforeAppend();
                    newBlock.SeriesAppend(key, value);

                    ds.AddLast(key, newBlock);
                    block = newBlock;
                    ThrowHelper.DebugAssert(Data == ds || data == ds);
                }

                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
                block = null;
                return false;
            }
        }

        /// <inheritdoc />
#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public bool TryAppend(TKey key, TValue value)
        {
            // We do not need to increment version in append as long as DataBlock counter
            // is incremented as the last and volatile operation (_rowCount is volatile).
            AcquireLock();
            try
            {
                return DangerousTryAppend(key, value);
            }
            finally
            {
                ReleaseLock();
            }
        }

        /// <inheritdoc />
#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public void Append(TKey key, TValue value)
        {
            if (!TryAppend(key, value))
            {
                ThrowCannotAppend(key, value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowCannotAppend(TKey key, TValue value)
        {
            if (Mutability == Mutability.ReadOnly)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot append values to read-only series.");
            }

            if (TryGetValue(key, out _) && KeySorting == KeySorting.Strong)
            {
                ThrowHelper.ThrowArgumentException($"Cannot append [{key}, {value}]. Key already exists.");
            }

            var last = Last;
            if (last.IsPresent)
            {
                var c = Comparer.Compare(key, Last.Present.Key);
                if ((c < 0 && KeySorting != KeySorting.NotSorted)
                    || (c == 0 && KeySorting == KeySorting.Strong))
                {
                    ThrowHelper.ThrowArgumentException($"Cannot append [{key}, {value}]. Key [{key}] would break sorting order {KeySorting}.");
                }
            }

            ThrowHelper.ThrowInvalidOperationException($"Cannot append [{key}, {value}].");
        }

        /// <inheritdoc />
#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public bool TryAppendMany<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
#if HAS_AGGR_OPT
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif

        public void AppendMany<TPairs>(TPairs pairs) where TPairs : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            if (!TryAppendMany(pairs))
            {
                ThrowCannotAppend();
            }
            void ThrowCannotAppend()
            {
                if (Mutability == Mutability.ReadOnly)
                {
                    ThrowHelper.ThrowInvalidOperationException("Cannot append values to read-only series.");
                }

                ThrowHelper.ThrowInvalidOperationException($"Cannot append key-value pair");
            }
        }

        /// <inheritdoc />
        public void MarkReadOnly()
        {
            Flags.MarkReadOnly();
        }

        internal class MovingWindowOptions : ISpreadsThreadPoolWorkItem
        {
            private readonly AppendSeries<TKey, TValue> _series;
            public readonly MovingWindowOptions<TKey> Options;
            public DataBlock? LingeringDataBlock;

            public MovingWindowOptions(AppendSeries<TKey, TValue> series, MovingWindowOptions<TKey> movingWindowOptions)
            {
                _series = series;
                Options = movingWindowOptions ?? throw new ArgumentNullException(nameof(movingWindowOptions));
            }

            public void OnBeforeAppend()
            {
                if (LingeringDataBlock != null
                    //&& Options is MovingWindowOptions<TKey, TValue> typedOpts
                    //&& typedOpts.OnRemovedHandler != null
                    )
                {
                    ThrowHelper.DebugAssert(Options is MovingWindowOptions<TKey, TValue> typedOpts
                                            && typedOpts.OnRemovedHandler != null);
                    SpreadsThreadPool.Background.UnsafeQueueCompletableItem(this, true);
                }
            }

            public void OnBeforeNewBlock()
            {
                if (_series.IsDataBlock(out _, out var ds))
                {
                    ThrowHelper.DebugAssert(false, "This method should not be called for data block case.");
                    return;
                }

                var blockSeries = ds._blockSeries;
                var level = 1;
                // find deepest DS level
                DataBlock? bsDb;
                while (!blockSeries.IsDataBlock(out bsDb, out var bsDs))
                {
                    if (bsDs._blockSeries.IsEmpty)
                    {
                        throw new NotImplementedException("TODO");
                    }
                    blockSeries = bsDs._blockSeries;
                    level++;
                }

                // bsDb is from where we should delete the first data block
                bsDb.SeriesTrimFirstValue<TKey, DataBlock>(out _, out _);

                var firstBlock = ds._blockSeries.First;

                if (AdditionalCorrectnessChecks.Enabled) { ThrowHelper.Assert(firstBlock.IsPresent); }

                var rc = firstBlock.Present.Value.ReferenceCount;

                Console.WriteLine($"RefCount: {rc}, level: {level}");
                //Console.WriteLine($"RefCountLast: {ds._blockSeries.Last.Present.Value.ReferenceCount}");
            }

            //public void OnBeforeNewBlock()
            //{
            //    if (LingeringDataBlock != null)
            //    {
            //        // TODO handle remaining block items and the block itself
            //        // before
            //    }
            //    if (Options != null
            //        )
            //    {
            //        _counter++;
            //    }
            //}

            public void Execute()
            {
                // we cannot trust delegates to call them from inside lock
                // also cleaning up items after removal is background job semantically

                var lingeringBlock = LingeringDataBlock;
                if (lingeringBlock != null)
                {
                    // TODO Array[2], not a single item. Not 3+, we should accelerate disposal
                }
            }
        }
    }
}
