// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spreads.Collections.Internal;

namespace Spreads.Collections
{
    public partial class DataContainer
    {
        /// <summary>
        /// When found, updates key by the found key if it is different, returns block and index whithin the block where the data resides.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="key"></param>
        /// <param name="lookup"></param>
        /// <param name="block"></param>
        /// <param name="blockIndex"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static bool TryFindBlockAt<TKey>(object data, ref TKey key,
            Lookup lookup,
            [NotNullWhen(returnValue: true)] out DataBlock? block,
            out int blockIndex,
            KeyComparer<TKey> comparer)
        {
            // This is non-obvious part:
            // For most searches we could find the right block
            // by searching with LE on the source:
            // o - EQ match, x - non-existing target key
            // | - start of a block, there is no space between start of block and it's first element (TODO review, had issues with that)
            // * for LE
            //      - [...] |[o...] |[<..]
            //      - [...] |[.o..] |[<..]
            //      - [...] |[ox..] |[<..]
            //      - [...] |[...o]x|[<..]
            // * for EQ
            //      - [...] |[x...] |[<..] - not possible, because with LE this would be [..o]x|[....] | [<..] since the first key must be LE, if it is not !EQ we find previous block
            //      - [...] |[o...] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..x.] |[<..]
            //      - [...] |[....]x|[<..]
            // * for GE
            //      - [...] |[o...] |[<..]
            //      - [...] |[xo..] |[<..]
            //      - [...] |[..o.] |[<..]
            //      - [...] |[..xo] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE, SLE+SS do not find key but it is in the next block if it exist
            //      - [...] |[....]x|[o..] SPECIAL CASE
            // * for GT
            //      - [...] |[xo..] |[<..]
            //      - [...] |[.xo.] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE
            //      - [...] |[..xo] |[<..]
            //      - [...] |[...x] |[o..] SPECIAL CASE
            //      - [...] |[....]x|[o..] SPECIAL CASE

            // for LT we need to search by LT

            // * for LT
            //      - [..o] |[x...] |[<..]
            //      - [...] |[ox..] |[<..]
            //      - [...] |[...o]x|[<..]

            // So the algorithm is:
            // Search source by LE or by LT if lookup is LT
            // Do SortedSearch on the block
            // If not found check if complement is after the end

            // Notes: always optimize for LE search, it should have least branches and could try speculatively
            // even if we could detect special cases in advance. Only when we cannot find check if there was a
            // special case and process it in a slow path as non-inlined method.

            bool retryOnGt = false;

            if (!IsDataBlock<TKey>(data, out block, out var ds))
            {
                retryOnGt = true;
                if (!TryFindBlockAtFromSource(out block, ds, key,
                    lookup == Lookup.LT ? Lookup.LT : Lookup.LE,
                    comparer))
                {
                    block = null;
                    blockIndex = -1;
                    return false;
                }
            }

            while (true)
            {
                blockIndex = block.LookupKey(ref key, lookup, comparer);

                if (blockIndex >= 0)
                {
                    return true;
                }

                // Check for SPECIAL CASE from the comment above
                if (retryOnGt
                    & /*not a type, single & */ ((int) lookup & (int) Lookup.GT) != 0
                    && ~blockIndex == block.RowCount)
                {
                    retryOnGt = false;
                    ThrowHelper.DebugAssert(ds != null, "retryOnGt is set only when ds != null");
                    if (ds!.TryGetNextBlock(block, out block))
                    {
                        continue;
                    }
                }

                break;
            }

            block = null;
            blockIndex = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
            | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static bool TryFindBlockAtFromSource<TKey>([NotNullWhen(true)] out DataBlock? db,
            DataBlockSource<TKey> ds,
            TKey key,
            Lookup sourceDirection,
            KeyComparer<TKey> comparer)
        {
            if (!ds.TryFindAt(key, sourceDirection, out var kvp))
            {
                db = null;
                return false;
            }

            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (kvp.Value.RowCount <= 0 ||
                    comparer.Compare(kvp.Key, kvp.Value.UnsafeGetRowKey<TKey>(index: 0)) != 0)
                {
                    ThrowBadBlockFromSource();
                }
            }

            db = kvp.Value;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected static void ThrowBadBlockFromSource()
        {
            ThrowHelper.ThrowInvalidOperationException("BaseContainer.DataSource.TryFindAt " +
                                                       "returned an empty block or key that doesn't match the first row index value");
        }
        
        
        
    }
}