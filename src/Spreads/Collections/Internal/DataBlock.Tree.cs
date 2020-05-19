// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Algorithms;

namespace Spreads.Collections.Internal
{
    internal sealed partial class DataBlock
    {
        // Increasing this value further brings no benefits on single-thread
        // micro-benchmark. On real load higher value will lead to more cache
        // trashing and misses, worse locality for sorted search and whatnot.
        //
        // Height 1: 4096^2 ~ 16 million items or 250 MB of payload with 8/8 KV size
        // Height 2: 4096^3 ~ 68 billion items or 1 TB of payload with 8/8 KV size
        // Height 3: 4096^4 ~ ... 4 PB (peta byte) of payload with 8/8 KV size, impossible on known modern hardware
        // 
        // The main relevant parameter is the threshold between height 2 and 3.
        // The only other good candidate is 8192 and it give 16 * (2^2) = 67 million items:
        // payload must be in 16-67 million items and mostly consist of sorted searches
        // and accessing by index (GetAt) to justify MaxNodeSize at 8192.  
        // 
        // MaxNodeSize of 2048 makes the height 1/2 threshold too small. Another consideration
        // is SIMD/batching performance that benefits from higher block size.
        // Therefore the value of 4096 is optimal as the initial choice.
        // We will need real load benches to see if 2048 or 8192 are better.
        // TODO (low) check 2048/8192 with batching calculation tree. 
#if DEBUG
        internal static int MaxNodeSize = 4096;
        internal static int NodeShift => 31 - BitUtil.NumberOfLeadingZeros(MaxNodeSize);
        internal static long NodeMask => (1 << NodeShift) - 1;
#else
        internal const int MaxNodeSize = 4096;
        internal const int NodeShift = 12;
        internal const long NodeMask = (1 << NodeShift) - 1;
#endif
        
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int GetAt(DataBlock root, long index, out DataBlock? block)
        {
            // TODO(!) this won't work when _head is not zero or block size (RowCount) is not equal

            unchecked
            {
                block = root;
                int i;
                while (true)
                {
                    i = (int) ((index >> (NodeShift * block.Height)) & NodeMask);
                    if (block.Height > 0)
                    {
                        var newBlock = block.Values.UnsafeReadUnaligned<DataBlock>(i);
                        ThrowHelper.DebugAssert(newBlock.Height == block.Height - 1);
                        block = newBlock;
                    }
                    else
                    {
                        break;
                    }
                }

                ThrowHelper.DebugAssert(block.Height == 0);
                ThrowHelper.DebugAssert(unchecked((uint) i) - block.Lo < unchecked((uint) (block.Hi + 1 - block.Lo)));
                return i;
            }
        }

        // [MethodImpl(MethodImplOptions.NoInlining)]
        // // ReSharper disable once UnusedParameter.Local
        // private static bool TryGetAtSlow(long index, out DataBlock? block, out int blockIndex)
        // {
        //     using (var bc = new BlockCursor<TKey, int, BaseContainer<TKey>>())
        //     {
        //         if (bc.Move(index, false) == 0)
        //         {
        //             block = null;
        //             blockIndex = -1;
        //             return false;
        //         }
        //
        //         block = bc.CurrentBlock;
        //         blockIndex = bc.BlockIndex;
        //         return true;
        //     }
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int SearchKey<T>(DataBlock root, T key, KeyComparer<T> comparer, out DataBlock? block)
        {
            unchecked
            {
                block = root;
                int i;
                while (true)
                {
                    int lo = block.Lo;
                    int hi = block.Hi;
                    ThrowHelper.Assert(lo >= 0 && hi >= 0);

                    i = VectorSearch.SortedSearchLoHi(ref block.RowKeys.UnsafeGetRef<T>(), lo, hi, key, comparer);
                    if (block.Height > 0)
                    {
                        ThrowHelper.DebugAssert(block.PreviousBlock == null);
                        ThrowHelper.DebugAssert(block.NextBlock == null);
                        // adjust for LE operation if needed
                        int ii;
                        if ((uint) (ii = ~i - 1) <= hi)
                            i = ii;

                        var newBlock = block.Values.UnsafeReadUnaligned<DataBlock>(i);
                        ThrowHelper.DebugAssert(newBlock.Height == block.Height - 1);
                        block = newBlock;
                    }
                    else
                    {
                        break;
                    }
                }

                ThrowHelper.DebugAssert(block.Height == 0);

                ThrowHelper.DebugAssert(unchecked((uint) i) - block.Lo < unchecked((uint) (block.Hi + 1 - block.Lo)));
                return i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static int LookupKey<T>(DataBlock root, ref T key, Lookup lookup, KeyComparer<T> comparer, out DataBlock? block)
        {
            unchecked
            {
                block = root;
                int lo;
                int hi;
                int i;
                while (true)
                {
                    lo = block.Lo;
                    hi = block.Hi;
                    ThrowHelper.Assert(lo >= 0 && hi >= 0);

                    i = VectorSearch.SortedSearchLoHi(ref block.RowKeys.UnsafeGetRef<T>(), lo, hi, key, comparer);
                    if (block.Height > 0)
                    {
                        
                        ThrowHelper.DebugAssert(block.PreviousBlock == null);
                        ThrowHelper.DebugAssert(block.NextBlock == null);
                        // adjust for LE operation if needed
                        int ii;
                        if ((uint) (ii = ~i - 1) <= hi)
                            i = ii;

                        if (i < 0) // cannot find LE block
                        {
                            // if GE or GT, get first available block
                            if ((lookup & Lookup.GT) != 0)
                                i = lo;
                            else
                                return -1;
                        }

                        var newBlock = block.Values.UnsafeReadUnaligned<DataBlock>(i);
                        ThrowHelper.DebugAssert(newBlock.Height == block.Height - 1);
                        block = newBlock;
                    }
                    else
                    {
                        break;
                    }
                }

                ThrowHelper.DebugAssert(block.Height == 0);

                if (i >= lo)
                {
                    if (lookup.IsEqualityOK())
                        goto RETURN_I;

                    if (lookup == Lookup.LT)
                    {
                        if (i == lo)
                            goto RETURN_PREV;

                        i--;
                    }
                    else // depends on if (eqOk) above
                    {
                        Debug.Assert(lookup == Lookup.GT);
                        if (i == hi)
                            goto RETURN_NEXT;

                        i++;
                    }
                }
                else
                {
                    if (lookup == Lookup.EQ)
                        goto RETURN_I;

                    i = ~i;

                    // LT or LE
                    if (((uint) lookup & (uint) Lookup.LT) != 0)
                    {
                        // i is idx of element that is larger, nothing here for LE/LT
                        if (i == lo)
                            goto RETURN_PREV;

                        i--;
                    }
                    else
                    {
                        Debug.Assert(((uint) lookup & (uint) Lookup.GT) != 0);
                        Debug.Assert(i <= hi + 1);
                        // if was negative, if it was ~length then there are no more elements for GE/GT
                        if (i == hi + 1)
                            goto RETURN_NEXT;

                        // i is the same, ~i is idx of element that is GT the value
                    }
                }

                UPDATE_KEY:
                key = block!.UnsafeGetRowKey<T>(i);

                RETURN_I:
                ThrowHelper.DebugAssert(unchecked((uint) i) - lo < unchecked((uint) (hi + 1 - lo)));
                return i;

                RETURN_PREV:
                block = block.PreviousBlock;
                i = block == null ? -1 : block.Hi;
                goto UPDATE_KEY;

                RETURN_NEXT:
                block = block.NextBlock;
                i = block == null ? -1 : 0;
                goto UPDATE_KEY;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static bool TryAppend<TKey, TValue>(DataBlock block, ref DataBlock lastBlock, TKey key, TValue value, KeyComparer<TKey> comparer, KeySorting keySorting)
        {
            if (block.Hi >= 0)
            {
                ThrowHelper.DebugAssert(lastBlock.Hi >= 0);
                var lastKey = lastBlock.UnsafeGetRowKey<TKey>(lastBlock.Hi);
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

            Append(block, ref lastBlock, key, value);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static void Append<TKey, TValue>(DataBlock block, ref DataBlock lastBlock, TKey key, TValue value)
        {
            ThrowHelper.DebugAssert(lastBlock != null);

            // KeySorting should be done at upper level

            if (!lastBlock.TryAppendToBlock(key, value, increaseCapacity: true))
                AppendNode(block, ref lastBlock, key, value);
        }

        internal static void AppendNode<TKey, TValue>(DataBlock root, ref DataBlock lastBlock, TKey key, TValue value)
        {
            AppendNode(root, key, value, out var blockToAdd, ref lastBlock);
            if (blockToAdd != null)
            {
                // The root block is full, we need to create a new
                // root, but must keep the object reference (e.g.
                // it's read-only field of Panel structs). 
                // To do so, we need to replace content of the root
                // with a new block that contains old root + blockToAdd + room for new blocks.
                // This should happen quite rarely.

                // content of the root moved to this new block
                var firstBlock = root.MoveInto();

                ThrowHelper.DebugAssert(firstBlock.Height == blockToAdd.Height);

                // now `block` is completely empty now
                // create a temp block using existing methods
                // and then move it into `block` and dispose 

                var tempRoot = CreateForSeries<TKey, DataBlock>();

                tempRoot._refCount = firstBlock._refCount; // copy back
                firstBlock._refCount = 1; // owned by root
                tempRoot.Height = firstBlock.Height + 1;

                if (firstBlock.ColumnKeys != default)
                    tempRoot.ColumnKeys = firstBlock.ColumnKeys.Clone();
                tempRoot.ColumnCount = firstBlock.ColumnCount;
                // TODO review .Columns

                // Avoid multiple calls to method marked with AggressiveInlining, opposite to unrolling
                // tempRoot.AppendToBlock(firstBlock.UnsafeGetRowKey<TKey>(firstBlock._head), firstBlock);
                // tempRoot.AppendToBlock(key, blockToAdd);
                ThrowHelper.DebugAssert(KeyComparer<TKey>.Default.Compare(blockToAdd.UnsafeGetRowKey<TKey>(blockToAdd.Lo), key) == 0);
                var appendBlock = firstBlock;
                for (int i = 0; i < 2; i++)
                {
                    tempRoot.AppendToBlock(appendBlock.UnsafeGetRowKey<TKey>(appendBlock.Lo), appendBlock);
                    appendBlock = blockToAdd;
                }

                if (firstBlock.Height == 0)
                {
                    // AppendNode treated root as height=0, but we rewrite
                    // it's content and need to update references
                    ThrowHelper.DebugAssert(blockToAdd.PreviousBlock == root);
                    blockToAdd.PreviousBlock = firstBlock;
                    ThrowHelper.DebugAssert(firstBlock.NextBlock == blockToAdd);
                }
                else
                {
                    ThrowHelper.DebugAssert(blockToAdd.PreviousBlock == null);
                    ThrowHelper.DebugAssert(firstBlock.NextBlock == null);
                }

                // firstBlock.NextBlock = blockToAdd; already
                ThrowHelper.DebugAssert(tempRoot.NextBlock == null && tempRoot.PreviousBlock == null);

                ThrowHelper.Assert(tempRoot.Lo == 0 && tempRoot.Hi == 1);

                tempRoot.MoveInto(root);
                tempRoot.Dispose();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="block"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="blockToAdd">Not null when a new block must be added at the same height as <paramref name="block"/></param>
        /// <param name="newlastBlock">Not null when a new last block with height zero is created.</param>
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static void AppendNode<TKey, TValue>(DataBlock block, TKey key, TValue value,
            out DataBlock? blockToAdd, ref DataBlock newlastBlock)
        {
            if (block.IsLeaf)
            {
                ThrowHelper.DebugAssert(block.Height == 0);

                if (block.TryAppendToBlock<TKey, TValue>(key, value, increaseCapacity: true))
                {
                    blockToAdd = null;
                    return;
                }

                ThrowHelper.DebugAssert(block.RowCount == MaxNodeSize);
                // block.Flags.MarkReadOnly();

                // TODO When adding new layout we could replace <K,V> with appender struct, like in GetValues

                var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxNodeSize);
                blockToAdd = CreateForSeries<TKey, TValue>(newRowCapacity);
                ThrowHelper.DebugAssert(blockToAdd.Height == 0);

                blockToAdd.AppendToBlock<TKey, TValue>(key, value);

                block.NextBlock = blockToAdd;
                blockToAdd.PreviousBlock = block;
                blockToAdd.Height = 0;
                blockToAdd._refCount = 1; // logically it is blockToAdd.Increment(), but we know it was zero and no-one uses it
                newlastBlock = blockToAdd;
            }
            else
            {
                var lastBlock = block.UnsafeGetValue<DataBlock>(block.Hi);
                ThrowHelper.DebugAssert(lastBlock.Height == block.Height - 1);

                // Recursive call. Instead of storing parent blocks
                // in a field or a stack we already have *the* stack.
                // Depths >4 is practically impossible with reasonable
                // fanout. Most common are 1 and 2, even 3 should be rare.
                AppendNode(lastBlock, key, value, out var newBlock, ref newlastBlock);

                if (newBlock != null)
                {
                    // If block has non-zero head it means some blocks are retired 
                    // in the moving window case. In that case we should behave  
                    // as if block is full and create a new block at the same height.
                    // This will minimize copying when removing retired blocks from upper levels.
                    // TODO this makes full block of different size and GetAt won't work
                    if (block.Lo != 0)
                        ThrowHelper.ThrowNotImplementedException();

                    if (block.Lo == 0 && block.TryAppendToBlock<TKey, DataBlock>(key, newBlock, increaseCapacity: true))
                    {
                        blockToAdd = null;
                    }
                    else
                    {
                        var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxNodeSize);
                        blockToAdd = CreateForSeries<TKey, DataBlock>(newRowCapacity);
                        blockToAdd.Height = block.Height;
                        blockToAdd._refCount = 1; // logically it is blockToAdd.Increment(), but we know it was zero and no-one uses it

                        blockToAdd.AppendToBlock<TKey, DataBlock>(key, newBlock);
                    }
                }
                else
                {
                    blockToAdd = null;
                }
            }
        }

        [Obsolete("This should do the whole tree work, not just block. Using VecSearch on RetainedVec is trivial")]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int LookupKey<T>(ref T key, Lookup lookup, KeyComparer<T> comparer = default)
        {
            return VectorSearch.SortedLookupLoHi(ref RowKeys.UnsafeGetRef<T>(), Lo, Hi, ref key, lookup, comparer);
        }

        [Obsolete("This should do the whole tree work, not just block. Using VecSearch on RetainedVec is trivial")]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int SearchKey<T>(T key, KeyComparer<T> comparer = default)
        {
            return VectorSearch.SortedSearchLoHi(ref RowKeys.UnsafeGetRef<T>(), Lo, Hi, key, comparer);
        }
    }
}