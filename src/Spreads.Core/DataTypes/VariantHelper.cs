// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.DataTypes {

    internal class VariantHelper<T> {
        public static TypeEnum TypeEnum = GetTypeEnum();

        /* https://github.com/dotnet/corefx/blob/master/src/System.Numerics.Vectors/src/System/Numerics/Vector.cs
        * PATTERN:
        *    if (typeof(T) == typeof(Int32)) { ... }
        *    else if (typeof(T) == typeof(Single)) { ... }
        * EXPLANATION:
        *    At runtime, each instantiation of Vector<T> will be type-specific, and each of these typeof blocks will be eliminated,
        *    as typeof(T) is a(JIT) compile-time constant for each instantiation.This design was chosen to eliminate any overhead from
        *    delegates and other patterns.
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeEnum GetTypeEnum() {
            // TODO check if return is the same as if else and the pattern above holds

            if (typeof(T) == typeof(bool)) { return TypeEnum.Bool; }
            if (typeof(T) == typeof(byte)) return TypeEnum.UInt8;
            if (typeof(T) == typeof(char)) return TypeEnum.UInt16; // as UInt16
            if (typeof(T) == typeof(sbyte)) return TypeEnum.Int8;
            if (typeof(T) == typeof(short)) return TypeEnum.Int16;
            if (typeof(T) == typeof(ushort)) return TypeEnum.UInt16;
            if (typeof(T) == typeof(int)) return TypeEnum.Int32;
            if (typeof(T) == typeof(uint)) return TypeEnum.UInt32;
            if (typeof(T) == typeof(long)) return TypeEnum.Int64;
            if (typeof(T) == typeof(ulong)) return TypeEnum.UInt64;
            if (typeof(T) == typeof(IntPtr)) return TypeEnum.Int64;
            if (typeof(T) == typeof(UIntPtr)) return TypeEnum.UInt64;
            if (typeof(T) == typeof(float)) return TypeEnum.Float32;
            if (typeof(T) == typeof(double)) return TypeEnum.Float64;
            if (typeof(T) == typeof(decimal)) return TypeEnum.Decimal;
            if (typeof(T) == typeof(Price)) return TypeEnum.Price;
            if (typeof(T) == typeof(Money)) return TypeEnum.Money;
            if (typeof(T) == typeof(DateTime)) return TypeEnum.DateTime;
            if (typeof(T) == typeof(Timestamp)) return TypeEnum.Timestamp;

            if (typeof(T) == typeof(Variant)) return TypeEnum.Variant;
            if (typeof(T) == typeof(Array)) return TypeEnum.Array; // TODO check if typeof(T).IsArray is any different

            // TODO known types, otherwise will fallback to fixed binary

#pragma warning disable 618
            if (TypeHelper<T>.Size > 0) return TypeEnum.FixedBinary;
#pragma warning restore 618

            //if (typeof(T) == typeof(object) && !value.Equals(default(T))) {
            //    return VariantHelper.GetTypeEnum(value.GetType());
            //}

            return TypeEnum.Object;
        }

        private static int _elementType = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetElementTypeEnum() {
            if (_elementType != -1) {
                return (TypeEnum)(byte)_elementType;
            }
            var ty = typeof(T);
            if (!ty.IsArray) {
                ThrowHelper.ThrowInvalidOperationException_ForVariantTypeMissmatch();
            }
            var elTy = ty.GetElementType();
            var elTypeEnum = VariantHelper.GetTypeEnum(elTy);
            _elementType = (int)elTypeEnum;
            return elTypeEnum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetElementTypeSize() {
            var ty = typeof(T);
            if (!ty.IsArray) {
                ThrowHelper.ThrowInvalidOperationException_ForVariantTypeMissmatch();
            }
            var elTy = ty.GetElementType();
#pragma warning disable 618
            return TypeHelper.GetSize(elTy);
#pragma warning restore 618
        }
    }

    public class VariantHelper {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetTypeEnum(Type ty) {
            // typeof(T) == typeof() is compile-time const

            if (ty == typeof(bool)) return TypeEnum.Bool;
            if (ty == typeof(byte)) return TypeEnum.UInt8;
            if (ty == typeof(char)) return TypeEnum.UInt16; // as UInt16
            if (ty == typeof(sbyte)) return TypeEnum.Int8;
            if (ty == typeof(short)) return TypeEnum.Int16;
            if (ty == typeof(ushort)) return TypeEnum.UInt16;
            if (ty == typeof(int)) return TypeEnum.Int32;
            if (ty == typeof(uint)) return TypeEnum.UInt32;
            if (ty == typeof(long)) return TypeEnum.Int64;
            if (ty == typeof(ulong)) return TypeEnum.UInt64;
            if (ty == typeof(IntPtr)) return TypeEnum.Int64;
            if (ty == typeof(UIntPtr)) return TypeEnum.UInt64;
            if (ty == typeof(float)) return TypeEnum.Float32;
            if (ty == typeof(double)) return TypeEnum.Float64;
            if (ty == typeof(decimal)) return TypeEnum.Decimal;
            if (ty == typeof(Price)) return TypeEnum.Price;
            if (ty == typeof(Money)) return TypeEnum.Money;
            if (ty == typeof(DateTime)) return TypeEnum.DateTime;
            if (ty == typeof(Timestamp)) return TypeEnum.Timestamp;

            if (ty == typeof(Variant)) return TypeEnum.Variant;
            if (ty.IsArray) return TypeEnum.Array; // TODO check if ty.IsArray is any different

            // TODO known types, otherwise will fallback to fixed binary

#pragma warning disable 618
            if (TypeHelper.GetSize(ty) > 0) return TypeEnum.FixedBinary;
#pragma warning restore 618

            return TypeEnum.Object;
        }
    }


    public static class VariantExtensions {
        public static Variant AsVariant<T>(this T value) {
            return Variant.Create<T>(value);
        }
    }
}
