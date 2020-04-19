// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct Flags
    {
        //  [context dependent][reserved][Mutability][KeySorting]

        private byte _value;

        public Flags(byte value)
        {
            _value = value;
        }

        public Flags(KeySorting keySorting, Mutability mutability)
        {
            _value = (byte)((byte)keySorting | (byte)mutability);
        }

        internal Flags(ContainerLayout layout, KeySorting keySorting, Mutability mutability)
        {
            _value = (byte)((byte)layout | (byte)keySorting | (byte)mutability);
        }

        public Mutability Mutability
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Mutability)(_value & (int)Mutability.Mutable);
        }

        public KeySorting KeySorting
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (KeySorting)(_value & (int)KeySorting.Strong);
        }

        internal ContainerLayout ContainerLayout
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ContainerLayout)(_value & (int)ContainerLayout.PanelFrameT);
        }

        public bool IsAppendOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)Mutability.Mutable) == (int)Mutability.AppendOnly;
        }

        // [Obsolete("This only checks is we could append, not ONLY append")]
        public bool CouldAppend
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)Mutability.AppendOnly) != 0;
        }

        public bool IsMutable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)Mutability.Mutable) != 0;
        }

        public bool IsImmutable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)Mutability.Mutable) == 0;
        }

        public bool IsStronglySorted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)KeySorting.Strong) != 0;
        }

        public bool IsWeaklySorted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)KeySorting.Weak) != 0;
        }

        public bool IsNotSorted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (int)KeySorting.Weak) == 0;
        }

        public void MarkAppendOnly()
        {
            if (IsImmutable)
            {
                ThrowHelper.ThrowInvalidOperationException("Already immutable");
            }
            _value &= 0b_1111_1101; // clear mutability bit
        }

        public void MarkReadOnly()
        {
            _value &= 0b_1111_1100; // clear mutability & append-only bits
        }

        //internal bool Is8thBitSet
        //{
        //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //    get => (_value & (int)0b_1000_0000) != 0;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal void Set8ThBit()
        //{
        //    _value |= 0b_1000_0000;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal void Clear8ThBit()
        //{
        //    _value &= 0b_01111_1111;
        //}
    }

    /// <summary>
    /// Mutability of underlying data source.
    /// </summary>
    [Flags]
    public enum Mutability : byte
    {
        /// <summary>
        /// Data cannot be modified.
        /// </summary>
        ReadOnly = 0b_0000_0000,

        /// <summary>
        /// Data could be added without changing existing order. Segments of existing data could be treated as <see cref="ReadOnly"/>.
        /// </summary>
        AppendOnly = 0b_0000_0001,

        /// <summary>
        /// Data could be modified at any place and order of existing data could change.
        /// </summary>
        Mutable = 0b_0000_0011
    }

    /// <summary>
    /// Determines how key order is enforced and if a data stream could be opened as series in case of <see cref="Strong"/> sorting
    /// or efficient search by keys is possible in case of <see cref="Weak"/> sorting.
    /// </summary>
    [Flags]
    public enum KeySorting : byte
    {
        /// <summary>
        /// Key sorting is not enforced but it may still be tracked and keys could be strongly or weakly sorted by accident.
        /// </summary>
        NotSorted = 0b_0000_0000,

        /// <summary>
        /// Keys are weakly monotonically sorted according to <see cref="KeyComparer{T}.Default"/> comparer.
        /// Repeating equal keys are possible.
        /// Search by a key always returns the first item with the same key (or throws NotSupportedException).
        /// </summary>
        Weak = 0b_0000_0100,

        /// <summary>
        /// Keys are strictly monotonically sorted according to <see cref="KeyComparer{T}.Default"/> comparer.
        /// No repeating keys are possible.
        /// </summary>
        Strong = 0b_0000_1100
    }

    // TODO this is inferrable from DataBlock, need to rework: Frame now means Panel,
    // but from layout POV vector, matrix and series are panels. Bits could be used 
    // to specify that RowKeys/ColumnKeys are typed.
    // A frame has non-zero columns in DataBlock
    [Flags]
    internal enum ContainerLayout : byte
    {
        /// <summary>
        /// Instance having this flag is none of the containers but a data stream or projection.
        /// It does not own DataBlock/DataBlockSource and does not inherit from BaseContainer.
        /// </summary>
        None = 0,

        /// <summary>
        /// TODO (review)
        /// Only contiguous (stride = 1) Values and
        /// borrowed columns pointing to the values storage with offset/stride IF NUMBER OF COLUMNS > 1.
        /// TODO (?) this is not required for virtual/projection? For a single column values with stride 1 is the column.
        /// </summary>
        Matrix = 0b_0001_0000,

        // TODO (TDB, review) just start working with those, will figure out during coding
        // Idea is that Series/Frame/Panels add features/properties (interface inheritance)
        // and if they have same storage layout

        /// <summary>
        /// Series is a matrix with single column and row index.
        /// </summary>
        Series = 0b_0011_0000,

        /// <summary>
        /// TODO What series owns storage but is not single-column matrix? Maybe a projection with the same keys but lazy values?
        /// E.g. vector math could use SIMD, but only for vertical operations and this could be done via MoveNextBatch.
        /// Frame/Panel could return columns with some projection.
        /// </summary>
        SeriesX = 0b_0010_0000,

        // Frame could always be used as series of rows
        Frame = 0b_0110_0000,

        FrameT = 0b_0111_0000,

        /// <summary>
        ///
        /// </summary>
        Panel = 0b_1000_0000,

        // ?? PanelSeries =  0b_0000_1010, it's about storage layout, not feature
        PanelFrame = 0b_1110_0000,

        PanelFrameT = 0b_1111_0000
    }
}