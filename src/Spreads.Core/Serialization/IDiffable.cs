using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// IDiffable&lt;T&gt; interface is implemented by types T for which
    /// difference could be represented as the type itself.
    /// E.g. all signed numeric types have this property (but the built-in
    /// one cannot implement this interface).
    /// Floating-point values could loose precision.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    //[Obsolete("TODO Use IDelta interface from Spreads.Unsafe")]
    //public interface IDiffable<T>
    //{
    //    /// <summary>
    //    /// Get delta.
    //    /// </summary>
    //    T GetDelta(T next);

    //    /// <summary>
    //    /// Add delta.
    //    /// </summary>
    //    T AddDelta(T delta);
    //}

    // NB to force diffing primitive types, one could use a struct like this
    // overheads are minimal

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