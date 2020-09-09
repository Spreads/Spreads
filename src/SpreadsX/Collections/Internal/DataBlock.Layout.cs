// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Threading;

namespace Spreads.Collections.Internal
{
    [StructLayout(LayoutKind.Explicit)]
    internal sealed partial class DataBlock 
    {
        [FieldOffset(0)]
        private volatile int _lo;

        [FieldOffset(4)]
        private volatile int _hi = -1;

        // This could be not equal to RowKeys.Length for Vector/Matrix.

        [FieldOffset(8)]
        private int _rowCapacity;

        [FieldOffset(12)]
        private int _columnCount;

        [FieldOffset(16)]
        private int _height;

        /// <summary>
        /// Primarily for cursors and circular implementation.
        /// We keep blocks used by cursors, which must
        /// decrement a block ref count when they no longer need it.
        /// </summary>
        [FieldOffset(20)]
        private int _refCount = AtomicCounter.Disposed;

        // _rowKeys should be at the and, rarely used fields act like padding
        [FieldOffset(24)]
        private DataBlock? _previousBlock;

        [FieldOffset(32)]
        private DataBlock? _nextBlock;

        [FieldOffset(40)]
        private RetainedVec[]? _columns;

        [FieldOffset(48)]
        private RetainedVec _columnKeys;

        [FieldOffset(80)]
        private RetainedVec _values;

        [FieldOffset(112)]
        private RetainedVec _rowKeys;

        /// <summary>
        /// Index of the first row.
        /// </summary>
        public int Lo
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _lo;
            private set
            {
                ThrowHelper.DebugAssert(value >= 0);
                _lo = value;
            }
        }

        /// <summary>
        /// Index of the last row.
        /// </summary>
        public int Hi
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hi;
            private set
            {
                ThrowHelper.DebugAssert(value >= _lo - 1);
                _hi = value;
            }
        }

        /// <summary>
        /// Current number of rows in this block.
        /// </summary>
        public int RowCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _hi - _lo + 1;
        }

        /// <summary>
        /// Max number of rows in this block.
        /// </summary>
        public int RowCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowCapacity;
            private set => _rowCapacity = value;
        }

        /// <summary>
        /// There is no free space beyond <see cref="Hi"/>.
        /// </summary>
        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Hi == _rowCapacity - 1;
        }

        /// <summary>
        /// Logical column count: 1 for Series, N for Panel (with _columns = null & row-major values) and Frames.
        /// </summary>
        public int ColumnCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _columnCount;
            private set => _columnCount = value;
        }

        /// <summary>
        /// Tree height, leafs have zero height.
        /// </summary>
        public int Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _height;
            private set => _height = value;
        }

        public bool IsLeaf => _height == 0;

        public int ReferenceCount => AtomicCounter.GetCount(ref _refCount);

        public int Increment()
        {
            return AtomicCounter.Increment(ref _refCount);
        }

        public int Decrement()
        {
            var newRefCount = AtomicCounter.Decrement(ref _refCount);
            if (newRefCount == 0)
                Dispose(true);

            return newRefCount;
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => AtomicCounter.GetIsDisposed(ref _refCount);
        }

        /// <summary>
        /// Fast path to get the previous block from the current one.
        /// </summary>
        /// <seealso cref="NextBlock"/>
        public DataBlock? PreviousBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _previousBlock;
            private set
            {
                ThrowHelper.DebugAssert(value == null || (value.Height == Height && value.Hi >= 0 && value.RowCount > 0));
                _previousBlock = value;
            }
        }

        /// <summary>
        /// Fast path to get the next block from the current one.
        /// </summary>
        /// <seealso cref="PreviousBlock"/>
        public DataBlock? NextBlock
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _nextBlock;
            private set
            {
                ThrowHelper.DebugAssert(value == null || (value.Height == Height && value.Hi >= 0 && value.RowCount > 0));
                _nextBlock = value;
            }
        }

        internal RetainedVec[]? Columns
        {
            get { return _columns; }
            set => _columns = value;
        }

        internal RetainedVec ColumnKeys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _columnKeys;
            set => _columnKeys = value;
        }

        internal RetainedVec Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _values;
            set => _values = value;
        }

        internal RetainedVec RowKeys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _rowKeys;
            set => _rowKeys = value;
        }
    }
}