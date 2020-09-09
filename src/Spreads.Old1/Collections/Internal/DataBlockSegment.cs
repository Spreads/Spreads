// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.Collections.Internal
{


    internal ref struct BlockPosition
    {
        public DataBlock DataBlock;
        public int Index;

        public long Move(long steps, bool allowPartial)
        {
            return Move(ref DataBlock, ref Index, steps, allowPartial);
        }
        
        public static long Move(ref DataBlock dataBlock, ref int index, long steps, bool allowPartial)
        {
            return -1;
        }
    }
    
    internal readonly ref struct DataSegment
    {
        public readonly DataBlock Block;
        public readonly int Lo;
        public readonly int Hi;
        public readonly Span<byte> Span;
    }
    
    /// <summary>
    /// Doubly linked list of borrowed <see cref="DataBlock"/>s.
    /// This struct does borrowing of the block provided to ctor and methods.
    /// It also borrows next and/or previous blocks of the initial block when needed.
    /// </summary>
    internal struct DataView : IDisposable
    {
        public DataBlock Root;
        
        public DataBlock FirstBlock;
        public DataBlock LastBlock;
        
        /// <summary>
        /// Index of the first element in the first block.
        /// </summary>
        public int FirstStart;

        /// <summary>
        /// Index of the last element in the last block.
        /// </summary>
        public int LastEnd;

        private long count;

        public void Initialize(DataBlock initialBlock, int initialElement)
        {
            ThrowHelper.Assert(initialElement < initialBlock.Hi);
            FirstBlock = LastBlock = initialBlock;
            FirstStart = LastEnd = initialElement;
            FirstBlock.Increment();
            LastBlock.Increment();
            count = 1;
        }

        public bool TryExpandLast()
        {
            var newLastEnd = LastEnd++;
            if (newLastEnd < LastBlock.RowCount)
            {
                LastEnd = newLastEnd;
                return true;
            }

            // TODO (a thought while writing this)
            // Cursors (that own this struct) must borrow DataBlockTree root, then it's safe
            // otherwise nasty races are possible
            
            var newLastBlock = LastBlock.NextBlock;
            if (newLastBlock != null)
            {
                newLastBlock.Increment();
                if (LastBlock == FirstBlock)
                    LastBlock.Decrement();
                LastBlock = newLastBlock;
                LastEnd = 0;
            }

            return false;
        }

        public void Dispose()
        {
            var block = FirstBlock.NextBlock;
            
            while (block != LastBlock)
            {
                
            }

            LastBlock.Decrement();
        }
    }
}