using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spreads.Algorithms;

namespace Spreads.Collections.Internal
{
    internal sealed partial class DataBlock
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int LookupKey<T>(T key, Lookup lookup, KeyComparer<T> comparer, out DataBlock? block)
        {
            block = this;
            int i;
            int ii;
            int length;
            while (true)
            {
                length = block.RowCount - _head;
                ThrowHelper.Assert(length > 0);

                i = VectorSearch.SortedSearch(ref block._rowKeys.UnsafeGetRef<T>(), _head, length, key, comparer);
                if (block.Height > 0)
                {
                    ThrowHelper.DebugAssert(block.PreviousBlock == null);
                    ThrowHelper.DebugAssert(block.NextBlock == null);
                    ThrowHelper.DebugAssert(block == this || block.LastBlock == null);
                    // adjust for LE operation if needed
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

                    var newBlock = block._values.UnsafeReadUnaligned<DataBlock>(i);
                    ThrowHelper.DebugAssert(newBlock.Height == block.Height - 1);
                    block = newBlock;
                }
                else
                {
                    break;
                }
            }

            // TODO 
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
        internal static void Append<TKey, TValue>(DataBlock block, TKey key, TValue value, out DataBlock? blockToAdd)
        {
            if (block.Height > 0)
            {
                var lastBlock = block.UnsafeGetValue<DataBlock>(block.RowCount - 1);
                ThrowHelper.DebugAssert(lastBlock.Height == block.Height - 1);
                
                Append(lastBlock, key, value, out var newBlock);
                
                if (newBlock != null)
                {
                    if (block.TryAppendToBlock(key, newBlock))
                    {
                        blockToAdd = null;
                    }
                    else
                    {
                        blockToAdd = DataBlock.CreateForPanel();
                        blockToAdd.AppendBlock(key, newBlock);
                    }
                }
                else
                {
                    blockToAdd = null;
                }
            }
            else
            {
                if (block.TryAppendToBlock(key, value))
                {
                    blockToAdd = null;
                }
                else
                {
                    blockToAdd = DataBlock.CreateForPanel();
                    blockToAdd.AppendBlock(key, value);
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
            return VectorSearch.SortedLookup(ref _rowKeys.UnsafeGetRef<T>(), offset: 0, RowCount, ref key, lookup, comparer);
        }

        [Obsolete("This should do the whole tree work, not just block. Using VecSearch on RetainedVec is trivial")]
        [MethodImpl(MethodImplOptions.AggressiveInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public int SearchKey<T>(T key, KeyComparer<T> comparer = default)
        {
            return VectorSearch.SortedSearch(ref _rowKeys.UnsafeGetRef<T>(), offset: 0, RowCount, key, comparer);
        }
    }
}