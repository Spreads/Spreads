using System;
using System.Runtime.CompilerServices;

namespace Spreads.Cursors.Internal
{
    internal partial class CursorImpl
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Move<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            // TODO called of this method should check CursorImpl lifecycle version with cached version if additional correctness checks are enabled

            // Hot path:
            // Try to move inside CurrentBlock. It could be Empty placeholder,
            // then we will do additional logic. But for rooted MN this attempt should
            // succeed in most cases.

            // Note: this does not handle MP from uninitialized state (_blockPosition == -1, stride <= 0). This case is rare.
            // Uninitialized multi-block case goes to rare as well as uninitialized MP
            var newBlockIndex = (long) (CurrentBlockIndex) + stride;
            
            // This branches should be predictable as false for rooted and batching cursors
            // DB.Empty has Hi = -1 and Lo = 0

            if (newBlockIndex > CurrentBlockHi)
                return Move2Next(stride, allowPartial, ref key, ref value);

            if (newBlockIndex < CurrentBlockLo)
                return Move2Previous(stride, allowPartial, ref key, ref value);

            CurrentBlockIndex = (int) newBlockIndex;
            GetCurrentBlockValues(CurrentBlock, (int) newBlockIndex, ref key, ref value);
            return stride;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2Next<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            if (stride == 0)
                return 0;
            
            return IsRooted 
                ? Move2NextRooted(stride, allowPartial, ref key, ref value) 
                : Move2NextComposite(stride, allowPartial, ref key, ref value);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2Previous<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            return IsRooted 
                ? Move2PreviousRooted(stride, allowPartial, ref key, ref value) 
                : Move2PreviousComposite(stride, allowPartial, ref key, ref value);
        }
        
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2NextRooted<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            ThrowHelper.DebugAssert(IsRooted);
            CurrentBlockHi = CurrentBlock.Hi;
            // TODO redo check from the fast path
            // TODO check if there is space in the block available and return false

            var newBlockIndex = (long) (CurrentBlockIndex) + stride;
            var newBlock = CurrentBlock;

            if (newBlockIndex <= CurrentBlockHi)
                // CurrentBlockHi increased after Move attempt
                goto SET_INDEX;
            
            // there is some space in the current block, do not lookup the next block
            // if (rowCount < CurrentBlock.RowCapacity & stride > 0)
            //     return 0;

            // This was implemented in BlockIndexCursor
            // TODO try to use local functions when copying

            SET_BLOCK:
            CurrentBlock = newBlock;
            SET_INDEX:
            CurrentBlockIndex = (int) newBlockIndex;
            GET_VALUES:
            GetCurrentBlockValues(newBlock, (int) newBlockIndex, ref key, ref value);
            return stride;
        }
        
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2NextComposite<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            ThrowHelper.DebugAssert(IsComposite);
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2PreviousRooted<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            ThrowHelper.DebugAssert(IsRooted);
            // It should be so that CB.Lo is never changed *for leafs* while anyone has a borrowed reference to the block
            // CurrentBlockLo = CurrentBlock.Lo;
            
            // This was implemented in BlockIndexCursor
            // TODO try to use local functions when copying
            throw new NotImplementedException();
        }
        
        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private int Move2PreviousComposite<K, V>(int stride, bool allowPartial, ref K key, ref V value)
        {
            ThrowHelper.DebugAssert(IsComposite);
            throw new NotImplementedException();
        }
    }
}