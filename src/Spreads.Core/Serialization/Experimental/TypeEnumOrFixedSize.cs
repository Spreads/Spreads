// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization.Experimental
{
    /// <summary>
    /// Known type enum or size of unknown fixed-length type.
    /// Abbreviated as TEOFS in code and comments.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 1, Pack = 1)]
    public readonly unsafe struct TypeEnumOrFixedSize
    {
        public const int MaxTypeEnum = 127;
        public const int MaxFixedSize = 128;
        public const int UnknownFixedSizeFlag = 128;

        private readonly byte _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeEnumOrFixedSize(TypeEnumEx typeEnum)
        {
            var value = (byte)typeEnum;
            if (value >= MaxTypeEnum) // LE to exclude virtual FixedBinary
            {
                SerializationThrowHelper.ThrowBadTypeEnum(value);
            }

            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeEnumOrFixedSize(byte unknownTypeSize)
        {
            var value = unknownTypeSize - 1;
            if (unchecked((uint)value) > MaxTypeEnum)
            {
                SerializationThrowHelper.ThrowFixedSizeOutOfRange(unknownTypeSize);
            }
            _value = (byte)(UnknownFixedSizeFlag | value);
        }

        public int Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetSize(_value);
        }

        public TypeEnumEx TypeEnum
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_value > 127)
                {
                    return TypeEnumEx.FixedBinary;
                }
                return (TypeEnumEx)_value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(byte typeEnumValue)
        {
            // Branchless is 3-5x slower and we pay the cost always.
            // Our goal is not to pay anything for known scalars
            // and the first 32 of them fit 1 cache line.
            // For other types we are OK with L1 miss or branch mis-prediction,
            // but only full lookup table does not impact known scalars.
            // L1 miss is less or ~same as wrong branch.

            #region Branchless

            // Interesting technique if we could often have L2 misses, but here it is slower than lookup.

            //var localSized = stackalloc int[4];
            //var val7Bit = (typeEnumValue & 0b_0111_1111) * (typeEnumValue >> 7);
            //// 00 - known scalar type
            //localSized[0] = _sizes[typeEnumValue & 0b_0011_1111]; // in L1
            //// 01 - var size or container
            //localSized[1] = -1;
            //// 10 - fixed size < 65
            //localSized[2] = (short)val7Bit;
            //// 11 fixed size [65;128]
            //localSized[3] = (short)val7Bit;
            //var localIdx = (typeEnumValue & 0b_1100_0000) >> 6;
            //return localSized[localIdx];

            #endregion Branchless

            return _sizes[typeEnumValue];
        }

        // Do not use static ctor, ensure beforefieldinit.
        private static readonly short* _sizes = TypeEnumHelper.Sizes;
    }
}