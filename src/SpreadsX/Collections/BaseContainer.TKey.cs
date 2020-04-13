// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    /// <summary>
    /// Base container with row keys of type <typeparamref name="TKey"/>.
    /// </summary>
    public class BaseContainer<TKey> : DataContainer
    {
        // internal ctor for tests only, it should have been abstract otherwise
        internal BaseContainer()
        { }

        protected internal KeyComparer<TKey> _comparer = default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsDataBlock([NotNullWhen(returnValue: true)] out DataBlock? dataBlock,
            [NotNullWhen(returnValue: false)] out DataBlockSource<TKey>? dataSource)
        {
            return IsDataBlock(Data, out dataBlock, out dataSource);
            // var d = Data;
            // // They are mutually exclusive so save one isinst call,
            // // and we could later replace isinst with bool field (there is padding space so it's free, need to benchmark)
            // if (d is DataBlock db)
            // {
            //     dataBlock = db;
            //     dataSource = null;
            //     return true;
            // }
            //
            // dataBlock = null;
            // dataSource = Unsafe.As<DataBlockSource<TKey>>(d);
            // return false;
        }
        
        

        /// <summary>
        /// Returns <see cref="DataBlock"/> that contains <paramref name="index"></paramref> and local index within the block as <paramref name="blockIndex"></paramref>.
        /// </summary>
        /// <param name="index">Index to get element at.</param>
        /// <param name="block"><see cref="DataBlock"/> that contains <paramref name="index"></paramref> or null if not found.</param>
        /// <param name="blockIndex">Local index within the block. -1 if requested index is not range.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBlockAt(long index, [NotNullWhen(returnValue: true)] out DataBlock block,
            out int blockIndex)
        {
            // Take reference, do not work directly. Reference assignment is atomic in .NET
            block = null;
            blockIndex = -1;
            var result = false;

            if (IsDataBlock(out var db, out var ds))
            {
                if (index < db.RowCount)
                {
                    block = db;
                    blockIndex = (int)index;
                    result = true;
                }
            }
            else
            {
                // TODO check DataBlock range, probably we are looking in the same block
                // But for this search it is possible only for immutable or append-only
                // because we need to track first global index. For such cases maybe
                // we should just guarantee that DataSource.ConstantBlockLength > 0 and is pow2.

                var constantBlockLength = ds.ConstantBlockLength;
                if (constantBlockLength > 0)
                {
                    // TODO review long division. constantBlockLength should be a poser of 2
                    var sourceIndex = index / constantBlockLength;
                    if (ds.TryGetAt(sourceIndex, out var kvp))
                    {
                        block = kvp.Value;
                        blockIndex = (int)(index - sourceIndex * constantBlockLength);
                        result = true;
                    }
                }
                else
                {
                    result = TryGetBlockAtSlow(index, out block, out blockIndex);
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // ReSharper disable once UnusedParameter.Local
        private static bool TryGetBlockAtSlow(long index, out DataBlock? block, out int blockIndex)
        {
            using (var bc = new BlockCursor<TKey, int, BaseContainer<TKey>>())
            {
                if (bc.Move(index, false) == 0)
                {
                    block = null;
                    blockIndex = -1;
                    return false;
                }

                block = bc.CurrentBlock;
                blockIndex = bc.BlockIndex;
                return true;
            }
        }

        /// <summary>
        /// Finds block and key index inside the block.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="block"></param>
        /// <param name="blockIndex"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal bool TryGetBlock(TKey key,
            [NotNullWhen(returnValue: true)] out DataBlock? block,
            out int blockIndex)
        {
            if (!IsDataBlock(out block, out var ds)
                && !TryGetBlockFromSource(out block, ds, in key))
            {
                block = null;
                blockIndex = -1;
                return false;
            }
            blockIndex = block.SearchKey(key, _comparer);
            return blockIndex >= 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private bool TryGetBlockFromSource([NotNullWhen(true)]out DataBlock? db,
            DataBlockSource<TKey> ds,
            in TKey key)
        {
            if (!ds.TryFindAt(key, Lookup.LE, out var kvp))
            {
                db = null;
                return false;
            }

            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (kvp.Value.RowCount <= 0 ||
                    _comparer.Compare(kvp.Key, kvp.Value.UnsafeGetRowKey<TKey>(index: 0)) != 0)
                { ThrowBadBlockFromSource(); }
            }

            db = kvp.Value;
            return true;
        }

        protected override void Dispose(object data, bool disposing)
        {
            if (IsDataBlock<TKey>(data, out var db, out var ds))
                db.Dispose();
            else
                ds.Dispose();

            Data = null!;

            base.Dispose(data, disposing: true);
        }
    }
}
