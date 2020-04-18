using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Spreads.Algorithms;

namespace Spreads.Collections.Internal
{
    internal sealed partial class DataBlock
    {
        internal static int MaxLeafSize = 4 * 4096;
        internal static int MaxNodeSize = 4096;

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int LookupKey<T>(ref T key, Lookup lookup, KeyComparer<T> comparer, out DataBlock? block)
        {
            block = this;
            int i;
            int length;
            while (true)
            {
                length = block.RowCount - _head;
                ThrowHelper.Assert(length > 0);

                i = VectorSearch.SortedSearch(ref block.RowKeys.UnsafeGetRef<T>(), _head, length, key, comparer);
                if (block.Height > 0)
                {
                    ThrowHelper.DebugAssert(block.PreviousBlock == null);
                    ThrowHelper.DebugAssert(block.NextBlock == null);
                    ThrowHelper.DebugAssert(block == this || block.LastBlock == null);
                    // adjust for LE operation if needed
                    int ii;
                    if ((uint) (ii = ~i - 1) < RowCount)
                        i = ii;

                    if (i < 0) // cannot find LE block
                    {
                        // if GE or GT, get first available block
                        if ((lookup & Lookup.GT) != 0)
                            i = _head;
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

            if (i >= _head)
            {
                if (lookup.IsEqualityOK())
                    goto RETURN_I;

                if (lookup == Lookup.LT)
                {
                    if (i == _head)
                        goto RETURN_PREV;

                    i--;
                }
                else // depends on if (eqOk) above
                {
                    Debug.Assert(lookup == Lookup.GT);
                    if (i == _head + length - 1)
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
                    if (i == _head)
                        goto RETURN_PREV;

                    i--;
                }
                else
                {
                    Debug.Assert(((uint) lookup & (uint) Lookup.GT) != 0);
                    Debug.Assert(i <= _head + length);
                    // if was negative, if it was ~length then there are no more elements for GE/GT
                    if (i == _head + length)
                        goto RETURN_NEXT;

                    // i is the same, ~i is idx of element that is GT the value
                }

                key = block.UnsafeGetRowKey<T>(i);
            }

            RETURN_I:
            ThrowHelper.DebugAssert(unchecked((uint) i) - _head < unchecked((uint) length));
            return i;

            RETURN_PREV:
            block = block.PreviousBlock;
            return block == null ? -1 : block.RowCount - 1;

            RETURN_NEXT:
            block = block.NextBlock;
            return block == null ? -1 : 0;
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static void Append<TKey, TValue>(DataBlock block, TKey key, TValue value)
        {
            var lastBlock = block.LastBlock;
            ThrowHelper.DebugAssert(lastBlock != null);
            ThrowHelper.DebugAssert((block.IsLeaf && block.LastBlock == block) || !block.IsLeaf);

            // TODO KeySorting check

            if (!lastBlock.TryAppendToBlock(key, value, increaseCapacity: true))
            {
                AppendNode(block, key, value);
            }
        }

        internal static void AppendNode<TKey, TValue>(DataBlock root, TKey key, TValue value)
        {
            AppendNode(root, key, value, out var blockToAdd);
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

                root.LastBlock = blockToAdd.LastBlock;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        internal static void AppendNode<TKey, TValue>(DataBlock block, TKey key, TValue value, out DataBlock? blockToAdd)
        {
            if (block.IsLeaf)
                blockToAdd = AppendLeaf();
            else
                blockToAdd = AppendNode();

            DataBlock? AppendLeaf()
            {
                ThrowHelper.DebugAssert(block.Height == 0);

                if (block.TryAppendToBlock<TKey, TValue>(key, value, increaseCapacity: true))
                {
                    // TODO we should never be here with LastBlock optimization
                    return null;
                }

                var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxLeafSize);
                var blockToAdd1 = CreateForSeries<TKey, TValue>(newRowCapacity);

                blockToAdd1.AppendToBlock<TKey, TValue>(key, value);

                block.NextBlock = blockToAdd1;
                blockToAdd1.PreviousBlock = block;

                ThrowHelper.Assert(block.LastBlock == block);
                block.LastBlock = null;
                
                // last leaf has self as last block, this bubbles up to the root
                blockToAdd1.LastBlock = blockToAdd1;

                blockToAdd1.Height = 0;
                return blockToAdd1;
            }

            DataBlock? AppendNode()
            {
                DataBlock? blockToAdd1;
                var lastBlock = block.UnsafeGetValue<DataBlock>(block.RowCount - 1);
                ThrowHelper.DebugAssert(lastBlock.Height == block.Height - 1);

                // Recursive call. Instead of storing parent blocks
                // in a field or a stack we already have *the* stack.
                // Depths >4 is practically impossible with reasonable
                // fanout. Most common are 1 and 2, even 3 should be rare.
                DataBlock.AppendNode(lastBlock, key, value, out var newBlock);

                if (newBlock != null)
                {
                    if (block.TryAppendToBlock<TKey, DataBlock>(key, newBlock, increaseCapacity: true))
                    {
                        block.LastBlock = newBlock.LastBlock;
                        blockToAdd1 = null;
                    }
                    else
                    {
                        var newRowCapacity = Math.Min(block.RowCapacity * 2, MaxNodeSize);
                        blockToAdd1 = CreateForSeries<TKey, DataBlock>(newRowCapacity);
                        blockToAdd1.Height = block.Height;
                        blockToAdd1.AppendToBlock<TKey, DataBlock>(key, newBlock);

                        block.LastBlock = null;
                        blockToAdd1.LastBlock = newBlock.LastBlock;
                    }
                }
                else
                {
                    blockToAdd1 = null;
                }

                return blockToAdd1;
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