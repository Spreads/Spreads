// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    [StructLayout(LayoutKind.Sequential, Size = sizeof(int))]
    public readonly struct RuntimeTypeId : IEquatable<RuntimeTypeId>
    {
        public readonly int TypeId;

        private RuntimeTypeId(int typeId)
        {
            if(typeId <= 0) ThrowHelper.ThrowArgumentException(nameof(typeId));
            TypeId = typeId;
        }

        public bool IsInvalid => TypeId <= 0;

        public static explicit operator RuntimeTypeId(int value) => new(value);

        public static implicit operator int(in RuntimeTypeId value) => value.TypeId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(RuntimeTypeId other) => TypeId == other.TypeId;

        public override bool Equals(object? obj) => obj is RuntimeTypeId other && Equals(other);

        public override int GetHashCode() => TypeId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(RuntimeTypeId lhs, RuntimeTypeId rhs)
        {
            return lhs.TypeId == rhs.TypeId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(RuntimeTypeId lhs, RuntimeTypeId rhs)
        {
            return lhs.TypeId != rhs.TypeId;
        }
    }
}
