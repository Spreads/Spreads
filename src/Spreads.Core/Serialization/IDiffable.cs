using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// IDiffable<T> interface is implemented by types T for which
    /// difference could be represented as the type itself.
    /// E.g. all signed numeric types have this property (but the built-in
    /// one cannot implement this interface).
    /// Floating-point values could loose precision.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDiffable<T>
    {
        T GetDelta(T next);

        T AddDelta(T delta);
    }

    // NB to force diffing primitive types, one could use a struct like this
    // overheads are minimal

    /// <summary>
    /// Diffable wrapper for decimal type
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct DecimalDiffable : IDiffable<DecimalDiffable>
    {
        public decimal Value;

        public DecimalDiffable(decimal value)
        {
            Value = value;
        }

        public DecimalDiffable AddDelta(DecimalDiffable delta)
        {
            return new DecimalDiffable(this.Value + delta.Value);
        }

        public DecimalDiffable GetDelta(DecimalDiffable next)
        {
            return new DecimalDiffable(next.Value - this.Value);
        }

        public static implicit operator DecimalDiffable(decimal value)
        {
            return new DecimalDiffable(value);
        }

        public static implicit operator decimal(DecimalDiffable value)
        {
            return value.Value;
        }

        public static DecimalDiffable[] ToDiffableArray(decimal[] decimalArray)
        {
            return Unsafe.As<DecimalDiffable[]>(decimalArray);
        }

        public static decimal[] ToDecimalArray(DecimalDiffable[] diffableArray)
        {
            return Unsafe.As<decimal[]>(diffableArray);
        }
    }
}