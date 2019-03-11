// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Known type enum or size of unknown fixed-length type.
    /// Abbreviated as TEOFS in code and comments.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 1, Pack = 1)]
    public readonly struct TypeEnumOrFixedSize : IEquatable<TypeEnumOrFixedSize>
    {
        public const int MaxScalarEnum = 63;
        public const int MaxTypeEnum = 127;
        public const int MaxFixedSize = 128;
        public const int UnknownFixedSizeFlag = 128;

        private readonly byte _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeEnumOrFixedSize(byte value, bool _)
        {
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeEnumOrFixedSize(TypeEnum typeEnum)
        {
            var value = (byte)typeEnum;
            if (value > MaxTypeEnum)
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

        /// <summary>
        /// Own size of a type.
        /// </summary>
        public short Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if SPREADS
            get => TypeEnumHelper.GetSize(_value);
#else
            get => 0; // Not used in Utf8Json
#endif
        }

        public TypeEnum TypeEnum
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_value > 127)
                {
                    return TypeEnum.FixedSize;
                }
                return (TypeEnum)_value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(TypeEnumOrFixedSize other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TypeEnumOrFixedSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(TypeEnumOrFixedSize x, TypeEnumOrFixedSize y)
        {
            return x.Equals(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(TypeEnumOrFixedSize x, TypeEnumOrFixedSize y)
        {
            return !x.Equals(y);
        }

        public override string ToString()
        {
            return $"{TypeEnum} - [{Size}]";
        }
    }
}