// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads
{
    // TODO (low) recollect why Nullable<T> was not good enough

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Opt<T> Present<T>(T value) // NB never create extensions on T
        {
            return new Opt<T>(value);
        }
    }

    /// <summary>
    /// A minimal implementation of the Option type. <typeparamref name="T"/> must implement <see cref="IEquatable{T}"/> for custom equality.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public struct Opt<T>
    {
        /// <summary>
        /// Missing value.
        /// </summary>
        public static Opt<T> Missing => default;

        private readonly T _present;
        private readonly byte _presence;

        /// <summary>
        /// Create new optional value with a given present value.
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Opt(in T value)
        {
            _presence = 1;
            _present = value;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //internal unsafe Opt(T value, bool presence)
        //{
        //    _presence = *(byte*)&presence;
        //    _present = value;
        //}

        /// <summary>
        /// True if a value is present.
        /// </summary>
        public readonly bool IsPresent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _presence != 0;
        }

        /// <summary>
        /// True if a value is missing.
        /// </summary>
        public readonly bool IsMissing
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _presence == 0;
        }

        /// <summary>
        /// Present value.
        /// </summary>
        public readonly T Present
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (AdditionalCorrectnessChecks.Enabled && IsMissing)
                    ThrowHelper.Assert((EqualityComparer<T>.Default.Equals(default, _present)));

                return _present;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T PresentOrDefault() => _present;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T PresentOrDefault(T defaultValue) => IsMissing ? defaultValue : _present;

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

        // Do not add implicit conversion to this direction, it's too easy to convert `default` to Present.
        // Instead, Opt.Present(...) is short enough and infers types automatically. Had bugs with this already
        // due to this implicit operator - do not add it back. Do not try to compare with default - be explicit.
        ///// <summary>
        ///// Implicit cast from a value of type <typeparamref name="T"/> to <see cref="Opt{T}"/>.
        ///// </summary>
        //public static explicit operator Opt<T>(T value)
        //{
        //    return new Opt<T>(value);
        //}

        /// <summary>
        /// Explicit cast from <see cref="Opt{T}"/> to a value of type <typeparamref name="T"/>.
        /// Return the default value if <see cref="Opt{T}"/> is missing.
        /// </summary>
        public static explicit operator T(Opt<T> optValue)
        {
            return optValue.PresentOrDefault();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal readonly bool TryGet(out T value)
        {
            if (_presence != 0)
            {
                value = Present;
                return true;
            }

            value = default!;
            return false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public readonly void Deconstruct(out bool isPresent, out T value)
        {
            isPresent = IsPresent;
            value = Present;
        }

        public override string ToString()
        {
            return IsMissing ? "{Opt: Missing}" : $"{{Opt: {Present?.ToString()}}}";
        }
    }

    public static class OptExtensions
    {
        public static T? AsNullable<T>(this Opt<T> opt) where T : struct
        {
            return opt.IsPresent ? (T?)opt.Present : null;
        }

        public static Opt<T> AsOpt<T>(this T? nullable) where T : struct
        {
            return nullable.HasValue ? Opt.Present(nullable.Value) : Opt<T>.Missing;
        }
    }
}
