using System;

namespace Spreads
{
    /// <summary>
    /// The minimal implementation of Option type/
    /// </summary>
    public struct Opt<T> : IEquatable<Opt<T>>
    {
        /// <summary>
        /// Missing value.
        /// </summary>
        public static readonly Opt<T> Missing = new Opt<T>();

        /// <summary>
        /// Create new optional value with a given present value.
        /// </summary>
        /// <param name="value"></param>
        public Opt(T value)
        {
            IsPresent = true;
            Value = value;
        }

        /// <summary>
        /// True if a value is present.
        /// </summary>
        public bool IsPresent { get; }

        /// <summary>
        /// True if a value is missing.
        /// </summary>
        public bool IsMissing => !IsPresent;

        /// <summary>
        /// Present value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Return a larger Opt value or Missing if both are missing. Missing value is treated as smaller than a present value.
        /// </summary>
        public static Opt<T> LargerOrMissing(Opt<T> first, Opt<T> second, KeyComparer<T> comparer)
        {
            if (first.IsMissing && second.IsMissing)
            {
                return Missing;
            }

            if (first.IsMissing) return second;
            if (second.IsMissing) return first;

            var c = comparer.Compare(first.Value, second.Value);

            return c >= 0 ? first : second;
        }

        /// <summary>
        /// Return a smaller Opt value or Missing if both are missing. Missing value is treated as larger than a present value.
        /// </summary>
        public static Opt<T> SmallerOrMissing(Opt<T> first, Opt<T> second, KeyComparer<T> comparer)
        {
            if (first.IsMissing && second.IsMissing)
            {
                return Missing;
            }

            if (first.IsMissing) return second;
            if (second.IsMissing) return first;

            var c = comparer.Compare(first.Value, second.Value);

            return c <= 0 ? first : second;
        }

        /// <inheritdoc />
        public bool Equals(Opt<T> other)
        {
            return IsPresent == other.IsPresent && (IsMissing || Value.Equals(other.Value));
        }
    }
}