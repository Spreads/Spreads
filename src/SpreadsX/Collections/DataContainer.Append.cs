// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spreads.Collections.Internal;

namespace Spreads.Collections
{
    public partial class DataContainer
    {
        [Obsolete("TODO Per key JIT-constant calculation")]
        private const int
            DefaultMaxBlockRowCount = 16_384; // Math.Max(Settings.MIN_POOLED_BUFFER_LEN, Settings.LARGE_BUFFER_LIMIT / Math.Max(Unsafe.SizeOf<TKey>(), Unsafe.SizeOf<TValue>()));

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static bool TryAppend<TKey, TValue>(DataContainer? container, ref object containerData, KeyComparer<TKey> comparer, KeySorting keySorting, TKey key, TValue value)
        {
            if (!IsDataBlock<TKey>(containerData, out var db, out var ds))
                db = ds.LastValueOrDefault!;

#if BUILTIN_NULLABLE
            Debug.Assert(db != null, "Data source must not be set if empty");
#endif
            var dbRowCount = db.RowCount;
            if (dbRowCount > 0)
            {
                var lastKey = db.UnsafeGetRowKey<TKey>(dbRowCount - 1);

                var c = comparer.Compare(key, lastKey);
                if (c <= 0 // faster path is c > 0
                    && (c < 0 & keySorting == KeySorting.Weak)
                    | // no short-circuit && or || here
                    (c == 0 & keySorting == KeySorting.Strong)
                )
                {
                    // TODO detect which condition caused that in Append.ThrowCannotAppend
                    return false;
                }
            }

            object? data = null;
            if (dbRowCount == db.RowCapacity)
            {
                if (!TryAppendGrowCapacity(container, containerData, key, value, ref db, out data))
                {
                    return false;
                }

                // increased capacity and added values
            }
            else
            {
                // WindowOptions?.OnBeforeAppend();
                db.AppendToBlock(key, value);
            }

            // Switch Data only after adding values to a data block.
            // Otherwise DS could have an empty block for a short
            // time and that requires a lot of special case handling
            // on readers' side.
            if (data != null)
                containerData = data;

            // TODO unchecked { Version++; }

            container?.NotifyUpdate();

            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static bool TryAppendGrowCapacity<TKey, TValue>(DataContainer? container, object containerData, TKey key, TValue value, [NotNullWhen(true)] ref DataBlock? block,
            out object? data)
        {
            ThrowHelper.DebugAssert(block != null);
            data = null;
            try
            {
                if (block == DataBlock.Empty)
                {
                    block = DataBlock.CreateForPanel(rowCount: 0);
                    data = block;
                }

                // TODO review: do we want buffers in LOH or not? <= vs <
                // next increment will be 64kb, avoid buffer in LOH

                // ReSharper disable once PossibleNullReferenceException
                if (block.RowCapacity < DefaultMaxBlockRowCount)
                {
                    if (block.IncreaseRowsCapacity<TKey, TValue>() < 0)
                    {
                        block = null;
                        return false;
                    }

                    // WindowOptions?.OnBeforeAppend();
                    block.AppendToBlock(key, value);
                }
                else
                {
                    // refactor switching to source logic to reuse in MutableSeries
                    if (IsDataBlock<TKey>(containerData, out var db, out var ds))
                    {
                        Debug.Assert(ReferenceEquals(block, db));

                        ds = new DataBlockSource<TKey>();
                        ds.AddLast(block.UnsafeGetRowKey<TKey>(0), block);
                        data = ds;
                    }

                    // before creating a new block try to remove first blocks that
                    // are not used and satisfy MovingWindowOptions
                    //if (!IsDataBlock(out _, out _)) 
                    //    WindowOptions?.OnBeforeNewBlock();

                    var minCapacity = block.RowCapacity;

                    // TODO this is lazy, 
                    var newBlock = DataBlock.CreateForPanel(rowCount: 0);
                    if (newBlock.IncreaseRowsCapacity<TKey, TValue>(minCapacity) < 0)
                    {
                        block = null;
                        return false;
                    }

                    // WindowOptions?.OnBeforeAppend();
                    newBlock.AppendToBlock(key, value);

                    ds.AddLast(key, newBlock);
                    block = newBlock;
                    ThrowHelper.DebugAssert((container == null || container.Data == ds) || data == ds);
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

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static bool TryAppendGrowCapacity<TKey, TValue>(DataContainer? container, DataBlock containerData, TKey key, TValue value, [NotNullWhen(true)] ref DataBlock? block,
            out DataBlock? data)
        {
            ThrowHelper.DebugAssert(block != null);
            data = null;
            try
            {
                if (block == DataBlock.Empty)
                {
                    block = DataBlock.CreateForPanel(rowCount: 0);
                    data = block;
                }

                // TODO review: do we want buffers in LOH or not? <= vs <
                // next increment will be 64kb, avoid buffer in LOH

                // ReSharper disable once PossibleNullReferenceException
                if (block.RowCapacity < DefaultMaxBlockRowCount)
                {
                    if (block.IncreaseRowsCapacity<TKey, TValue>() < 0)
                    {
                        block = null;
                        return false;
                    }

                    // WindowOptions?.OnBeforeAppend();
                    block.AppendToBlock(key, value);
                }
                else
                {
                    // refactor switching to source logic to reuse in MutableSeries
                    if (IsDataLeaf<TKey>(containerData, out var db, out var ds))
                    {
                        Debug.Assert(ReferenceEquals(block, db));

                        // TODO Create new block with new height

                        var newHeight = db.Height + 1;
                        DataBlock newBlockX = default;

                        ds = new DataBlockSource2<TKey>(containerData);

                        ds.AddLast(block.UnsafeGetRowKey<TKey>(0), block);

                        data = ds.DataRoot;
                    }

                    // before creating a new block try to remove first blocks that
                    // are not used and satisfy MovingWindowOptions
                    //if (!IsDataBlock(out _, out _)) 
                    //    WindowOptions?.OnBeforeNewBlock();

                    var minCapacity = block.RowCapacity;
                    var newBlock = DataBlock.CreateForPanel(rowCount: 0);
                    if (newBlock.IncreaseRowsCapacity<TKey, TValue>(minCapacity) < 0)
                    {
                        block = null;
                        return false;
                    }

                    // WindowOptions?.OnBeforeAppend();
                    newBlock.AppendToBlock(key, value);

                    ds.AddLast(key, newBlock);

                    // TODO should it be below block = newBlock;?
                    // The whole operation must be atomic, updating container root should be the last operation
                    if (containerData != ds.DataRoot)
                        data = ds.DataRoot;

                    block = newBlock;
                    ThrowHelper.DebugAssert((container == null || container.Data == ds.DataRoot) || data == ds.DataRoot);
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
    }
}