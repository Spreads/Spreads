// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    // TODO
    // Mutability 2 bits
    // KeySorting 2 bits
    // 4 bits left
    // Use Mask + shift, keep enums starting at 0. Or shift bits but still need a mask to compare with zero, but could quickly check
    // for exact, e.g. Flags & KeySorting.Strong != 0

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct Flags
    {
        //  [context dependent][reserved][Mutability][KeySorting]

        private byte _value;

        public Flags(byte value)
        {
            _value = value;
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

        public bool IsAppend
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
            get => (_value & (int)Mutability.AppendOnly) == 0;
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
    /// Mutability of underlying data storage.
    /// </summary>
    [Flags]
    public enum Mutability : byte
    {
        /// <summary>
        /// Data cannot be modified.
        /// </summary>
        Immutable = 0b_0000_0000,

        /// <summary>
        /// Data could be added without changing existing order. Segments of existing data could be treated as <see cref="Immutable"/>.
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
        /// Data is not sorted by keys (or sorted by accident).
        /// </summary>
        NotSorted = 0b_0000_0000,

        /// <summary>
        /// Keys are weakly monotonically sorted according to <see cref="KeyComparer{T}.Default"/> comparer.
        /// Repeating equal keys are possible.
        /// Search by key always returns the first item with the same key. (TODO)
        /// </summary>
        Weak = 0b_0000_0100,

        /// <summary>
        /// Keys are strictly monotonically sorted according to <see cref="KeyComparer{T}.Default"/> comparer.
        /// No repeating keys are possible.
        /// </summary>
        Strong = 0b_0000_1100
    }
}