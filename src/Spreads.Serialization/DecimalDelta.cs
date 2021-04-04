﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Diffable wrapper for decimal type
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    [BinarySerialization(blittableSize: 16)]
    internal unsafe struct DecimalDelta : IDelta<DecimalDelta>
    {
        public long v1;
        public long v2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta(decimal value)
        {
            this = default;
            var ptr = Unsafe.AsPointer(ref this);
            *(decimal*)ptr = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta AddDelta(DecimalDelta delta)
        {
            return new DecimalDelta(this + delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta GetDelta(DecimalDelta next)
        {
            return new DecimalDelta(next - this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DecimalDelta(decimal value)
        {
            return new DecimalDelta(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator decimal(DecimalDelta value)
        {
            return *(decimal*)Unsafe.AsPointer(ref value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DecimalDelta[] ToDeltaArray(decimal[] decimalArray)
        {
            return Unsafe.As<DecimalDelta[]>(decimalArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal[] ToDecimalArray(DecimalDelta[] deltaArray)
        {
            return Unsafe.As<decimal[]>(deltaArray);
        }
    }
}
