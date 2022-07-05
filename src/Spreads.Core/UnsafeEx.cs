// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;
// ReSharper disable UnusedParameter.Global

namespace Spreads
{
    public static class UnsafeEx
    {
        /// <summary>
        /// Calls <see cref="IComparable{T}.CompareTo(T)"/> method on a generic <paramref name="left"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IComparable{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareToConstrained<T>(ref T left, ref T right)
        {
            Ldarg_0();
            Ldarg_1();
            Ldobj<T>();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IComparable<T>>(), nameof(IComparable<T>.CompareTo)));
            Ret();
            throw IL.Unreachable();
        }

        // The above is the same as this, but without a type constraint. "Bad things" will happen if the type is not correct.
        // Same approach for other methods below.
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static int CompareToConstrained<T>(ref T left, ref T right) where T : IComparable<T>
        // {
        //     return left.CompareTo(right);
        // }

        /// <summary>
        /// Calls <see cref="IEquatable{T}.Equals(T)"/> method on a generic <paramref name="left"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IEquatable{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EqualsConstrained<T>(ref T left, ref T right)
        {
            Ldarg_0();
            Ldarg_1();
            Ldobj<T>();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IEquatable<T>>(), nameof(IEquatable<T>.Equals)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="object.GetHashCode"/> method on a generic <paramref name="obj"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCodeConstrained<T>(ref T obj)
        {
            Ldarg_0();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<object>(), nameof(GetHashCode)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="IDisposable.Dispose"/> method on a generic <paramref name="obj"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IDisposable"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeConstrained<T>(ref T obj)
        {
            Ldarg_0();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IDisposable>(), nameof(IDisposable.Dispose)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="IDelta{T}.AddDelta"/> method on a generic <paramref name="obj"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IDelta{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddDeltaConstrained<T>(ref T obj, ref T delta)
        {
            Ldarg_0();
            Ldarg_1();
            Ldobj<T>();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IDelta<T>>(), nameof(IDelta<T>.AddDelta)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="IDelta{T}.GetDelta"/> method on a generic <paramref name="obj"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IDelta{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDeltaConstrained<T>(ref T obj, ref T other)
        {
            Ldarg_0();
            Ldarg_1();
            Ldobj<T>();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IDelta<T>>(), nameof(IDelta<T>.GetDelta)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="IInt64Diffable{T}.Add"/> method on a generic <paramref name="obj"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IInt64Diffable{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AddLongConstrained<T>(ref T obj, long delta)
        {
            Ldarg_0();
            Ldarg_1();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IInt64Diffable<T>>(), nameof(IInt64Diffable<T>.Add)));
            Ret();
            throw IL.Unreachable();
        }

        /// <summary>
        /// Calls <see cref="IInt64Diffable{T}.Diff"/> method on a generic <paramref name="left"/> with the <seealso cref="OpCodes.Constrained"/> IL instruction.
        /// If the type <typeparamref name="T"/> does not implement <see cref="IInt64Diffable{T}"/> bad things will happen.
        /// Use static readonly bool field in a generic class that caches reflection check if the type implements the interface.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long DiffLongConstrained<T>(ref T left, ref T right)
        {
            Ldarg_0();
            Ldarg_1();
            Ldobj<T>();
            Constrained<T>();
            Callvirt(MethodRef.Method(TypeRef.Type<IInt64Diffable<T>>(), nameof(IInt64Diffable<T>.Diff)));
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadUnaligned<T>(ref T source)
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref source));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUnaligned<T>(ref T destination, T value)
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination), value);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceq(int first, int second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Ceq();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cgt(int first, int second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Cgt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clt(int first, int second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Clt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceq(long first, long second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Ceq();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cgt(long first, long second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Cgt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clt(long first, long second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Clt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceq(nint first, nint second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Ceq();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cgt(nint first, nint second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Cgt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clt(nint first, nint second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Clt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceq(float first, float second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Ceq();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cgt(float first, float second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Cgt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clt(float first, float second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Clt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ceq(double first, double second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Ceq();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cgt(double first, double second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Cgt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clt(double first, double second)
        {
            Ldarg_0();
            Ldarg_1();
            IL.Emit.Clt();
            Ret();
            throw IL.Unreachable();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BoolAsInt(bool boolValue)
        {
            Ldarg_0();
            Ret();
            throw IL.Unreachable();
        }
    }
}
