// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace Spreads.Collections
{
    /// <summary>
    /// Alias for int64 with explicit conversions to it.
    /// </summary>
    public struct Offset
    {
        private readonly long _value;

        private Offset(long value)
        {
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Offset(long value)
        {
            return new Offset(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator long(Offset offset)
        {
            return offset._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Offset operator +(Offset offset, long length)
        {
            return new Offset(checked(offset._value + length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Offset operator -(Offset offset, long length)
        {
            return new Offset(checked(offset._value - length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Offset offset, long value)
        {
            return offset._value < value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Offset offset, long value)
        {
            return offset._value > value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Offset offset, long value)
        {
            return offset._value == value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Offset offset, long value)
        {
            return !(offset == value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Offset operator ~(Offset offset)
        {
            return new Offset(~offset._value);
        }

        public bool Equals(Offset other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Offset && Equals((Offset)obj);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}