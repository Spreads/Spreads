using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Algorithms;

namespace Spreads.Collections.Internal
{
    internal sealed partial class DataBlock
    {
        // TODO this affects copying cost for small series, with height > 0 we stop copying
        internal static int MaxLeafSize = 4096;
        internal static int MaxNodeSize = MaxLeafSize;

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
                    var length = block.RowCount - block._head;
                    ThrowHelper.Assert(length > 0);

                    i = VectorSearch.SortedSearch(ref block.RowKeys.UnsafeGetRef<T>(), block._head, length, key, comparer);
                    if (block.Height > 0)
                    {
                        ThrowHelper.DebugAssert(block.PreviousBlock == null);
                        ThrowHelper.DebugAssert(block.NextBlock == null);
                        // adjust for LE operation if needed
                        int ii;
                        if (i < 0 && (uint) (ii = ~i - 1) < block.RowCount)
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
                
                ThrowHelper.DebugAssert(unchecked((uint) i) - block._head < unchecked((uint) (block.RowCount - block._head)));
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
                int i;
                while (true)
                {
                    var length = block.RowCount - block._head;
                    ThrowHelper.Assert(length > 0);

                    i = VectorSearch.SortedSearch(ref block.RowKeys.UnsafeGetRef<T>(), block._head, length, key, comparer);
                    if (block.Height > 0)
                    {
                        ThrowHelper.DebugAssert(block.PreviousBlock == null);
                        ThrowHelper.DebugAssert(block.NextBlock == null);
                        // adjust for LE operation if needed
                        int ii;
                        if ((uint) (ii = ~i - 1) < block.RowCount)
                            i = ii;

                        if (i < 0) // cannot find LE block
                        {
                            // if GE or GT, get first available block
                            if ((lookup & Lookup.GT) != 0)
                                i = block._head;
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

                if (i >= block._head)
                {
                    if (lookup.IsEqualityOK())
                        goto RETURN_I;

                    if (lookup == Lookup.LT)
                    {
                        if (i == block._head)
                            goto RETURN_PREV;

                        i--;
                    }
                    else // depends on if (eqOk) above
                    {
                        Debug.Assert(lookup == Lookup.GT);
                        if (i == block.RowCount - 1)
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
                        if (i == block._head)
                            goto RETURN_PREV;

                        i--;
                    }
                    else
                    {
                        Debug.Assert(((uint) lookup & (uint) Lookup.GT) != 0);
                        Debug.Assert(i <= block.RowCount);
                        // if was negative, if it was ~length then there are no more elements for GE/GT
                        if (i == block.RowCount)
                            goto RETURN_NEXT;

                        // i is the same, ~i is idx of element that is GT the value
                    }
                }

                UPDATE_KEY:
                key = block.UnsafeGetRowKey<T>(i);

                RETURN_I:
                ThrowHelper.DebugAssert(unchecked((uint) i) - block._head < unchecked((uint) (block.RowCount - block._head)));
                return i;

                RETURN_PREV:
                block = block.PreviousBlock;
                i = block == null ? -1 : block.RowCount - 1;
                goto UPDATE_KEY;
                
                RETURN_NEXT:
                block = block.NextBlock;
                i =  block == null ? -1 : 0;
                goto UPDATE_KEY;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
                ThrowHelper.DebugAssert(KeyComparer<TKey>.Default.Compare(blockToAdd.UnsafeGetRowKey<TKey>(blockToAdd._head), key) == 0);
                var appendBlock = firstBlock;
                for (int i = 0; i < 2; i++)
                {
                    tempRoot.AppendToBlock(appendBlock.UnsafeGetRowKey<TKey>(appendBlock._head), appendBlock);
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

                ThrowHelper.Assert(tempRoot.RowCount == 2);

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

                var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxLeafSize);
                blockToAdd = CreateForSeries<TKey, TValue>(newRowCapacity);
                ThrowHelper.DebugAssert(blockToAdd.Height == 0);
                
                blockToAdd.AppendToBlock<TKey, TValue>(key, value);

                block.NextBlock = blockToAdd;
                blockToAdd.PreviousBlock = block;
                blockToAdd.Height = 0;
                blockToAdd._refCount = 1;
                newlastBlock = blockToAdd;
            }
            else
            {
                var lastBlock = block.UnsafeGetValue<DataBlock>(block.RowCount - 1);
                ThrowHelper.DebugAssert(lastBlock.Height == block.Height - 1);

                // Recursive call. Instead of storing parent blocks
                // in a field or a stack we already have *the* stack.
                // Depths >4 is practically impossible with reasonable
                // fanout. Most common are 1 and 2, even 3 should be rare.
                AppendNode(lastBlock, key, value, out var newBlock, ref newlastBlock);

                if (newBlock != null)
                {
                    if (block.TryAppendToBlock<TKey, DataBlock>(key, newBlock, increaseCapacity: true))
                    {
                        blockToAdd = null;
                    }
                    else
                    {
                        var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxNodeSize);
                        blockToAdd = CreateForSeries<TKey, DataBlock>(newRowCapacity);
                        blockToAdd.Height = block.Height;
                        blockToAdd._refCount = 1;
                        
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
            return VectorSearch.SortedLookup(ref RowKeys.UnsafeGetRef<T>(), offset: 0, RowCount, ref key, lookup, comparer);
        }

        [Obsolete("This should do the whole tree work, not just block. Using VecSearch on RetainedVec is trivial")]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int SearchKey<T>(T key, KeyComparer<T> comparer = default)
        {
            return VectorSearch.SortedSearch(ref RowKeys.UnsafeGetRef<T>(), offset: 0, RowCount, key, comparer);
        }
    }
}