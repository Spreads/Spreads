using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    // TODO xml docs
    /// <summary>
    /// The minimal implementation of Option type. T must implement IEquitable for custom equality.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Opt<T> // : IEquatable<Opt<T>>
    {
        /// <summary>
        /// Missing value.
        /// </summary>
        public static Opt<T> Missing => default;

        private readonly int _presence; // NB with auto layout it will take at least 4 bytes anyway
        private readonly T _present;

        /// <summary>
        /// Create new optional value with a given present value.
        /// </summary>
        /// <param name="value"></param>
        public Opt(in T value)
        {
            _presence = 1;
            _present = value;
        }

        internal Opt(T value, int presence)
        {
            _presence = presence | 1;
            _present = value;
        }

        /// <summary>
        /// True if a value is present.
        /// </summary>
        public bool IsPresent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _presence != 0; }
        }

        /// <summary>
        /// True if a value is missing.
        /// </summary>
        public bool IsMissing
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _presence == 0; }
        }

        /// <summary>
        /// Present value.
        /// </summary>
        public T Present
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                #if DEBUG
                if (IsMissing)
                {
                    throw new InvalidOperationException("Cannot access Opt<>.Present when value is missing");
                }
                #endif
                return _present;
            }
        }

        internal int _Presence
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _presence; }
        }

        /// <summary>
        /// Return a larger Opt value or Missing if both are missing. Missing value is treated as smaller than a present value.
        /// </summary>
        public static Opt<T> LargerOrMissing(Opt<T> first, Opt<T> second, KeyComparer<T> comparer)
        {
            if (first.IsMissing && second.IsMissing)
            {
                return default;
            }

            if (first.IsMissing) return second;
            if (second.IsMissing) return first;

            var c = comparer.Compare(first.Present, second.Present);

            return c >= 0 ? first : second;
        }

        /// <summary>
        /// Return a smaller Opt value or Missing if both are missing. Missing value is treated as larger than a present value.
        /// </summary>
        public static Opt<T> SmallerOrMissing(Opt<T> first, Opt<T> second, KeyComparer<T> comparer)
        {
            if (first.IsMissing && second.IsMissing)
            {
                return default;
            }

            if (first.IsMissing) return second;
            if (second.IsMissing) return first;

            var c = comparer.Compare(first.Present, second.Present);

            return c <= 0 ? first : second;
        }

//        /// <inheritdoc />
//        public bool Equals(Opt<T> other)
//        {
//            return IsPresent == other.IsPresent
//                   && (IsMissing || KeyEqualityComparer<T>.EqualsStatic(Present, other.Present));
//        }

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
            return optValue.IsPresent ? optValue.Present : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal bool TryGet(out T value)
        {
            if (_presence != 0)
            {
                value = Present;
                return true;
            }

            value = default;
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Deconstruct(out bool isPresent, out T value)
        {
            isPresent = IsPresent;
            value = Present;
        }
    }
}