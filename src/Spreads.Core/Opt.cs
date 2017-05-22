using System;

namespace Spreads
{
    /// <summary>
    /// Helper methods for <see cref="Opt{T}"/>.
    /// </summary>
    public static class Opt
    {
        /// <summary>
        /// Crate new <see cref="Opt{T}"/> value.
        /// </summary>
        /// <example>
        /// Avoid type specification of <see cref="Opt{T}"/> constructor and just use the <see cref="Opt.Present{T}"/>
        /// method that infers the type and creates a new <see cref="Opt{T}"/> value with the given present value.
        /// <code>
        /// public static Opt&lt;int&gt; Example()
        /// {
        ///     return Opt.Present(1);
        /// }
        /// </code>
        /// </example>
        public static Opt<T> Present<T>(T value) // NB never create extensions on T
        {
            return new Opt<T>(value);
        }
    }

    /// <summary>
    /// The minimal implementation of Option type.
    /// </summary>
    public struct Opt<T> : IEquatable<Opt<T>>
    {
        /// <summary>
        /// Missing value.
        /// </summary>
        public static readonly Opt<T> Missing = new Opt<T>();

        /// <summary>
        /// Crate new <see cref="Opt{T}"/> value.
        /// </summary>
        public static Opt<T> Present(T value)
        {
            return new Opt<T>(value);
        }

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

        /// <summary>
        /// Implicit cast from a value of type <typeparamref name="T"/> to <see cref="Opt{T}"/>.
        /// </summary>
        public static implicit operator Opt<T>(T value)
        {
            return new Opt<T>(value);
        }

        /// <summary>
        /// Explicit cast from <see cref="Opt{T}"/> to a value of type <typeparamref name="T"/>.
        /// Return the default value if <see cref="Opt{T}"/> is missing.
        /// </summary>
        public static explicit operator T(Opt<T> optValue)
        {
            return optValue.IsPresent ? optValue.Value : default(T);
        }
    }
}