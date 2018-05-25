using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Diffable wrapper for decimal type
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct DecimalDelta : IDelta<DecimalDelta>
    {
        public decimal Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta(decimal value)
        {
            Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta AddDelta(DecimalDelta delta)
        {
            return new DecimalDelta(Value + delta.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DecimalDelta GetDelta(DecimalDelta next)
        {
            return new DecimalDelta(next.Value - Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator DecimalDelta(decimal value)
        {
            return new DecimalDelta(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator decimal(DecimalDelta value)
        {
            return value.Value;
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