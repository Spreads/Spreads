// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spreads.DataTypes
{
    internal class VariantHelper<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        public static readonly TypeEnum TypeEnum = GetTypeEnum();
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool IsInline = (int)GetTypeEnum() < Variant.KnownSmallTypesLimit;

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
        private static TypeEnum GetTypeEnum()
        {
            // TODO check if return is the same as if else and the pattern above holds

            if (typeof(T) == typeof(bool)) return TypeEnum.Bool;
            if (typeof(T) == typeof(byte)) return TypeEnum.UInt8;
            if (typeof(T) == typeof(char)) return TypeEnum.UInt16;
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
            //if (typeof(T) == typeof(Money)) return TypeEnum.Money;
            if (typeof(T) == typeof(DateTime)) return TypeEnum.DateTime;
            if (typeof(T) == typeof(Timestamp)) return TypeEnum.Timestamp;

            if (typeof(T) == typeof(Id)) return TypeEnum.Id;
            if (typeof(T) == typeof(Symbol)) return TypeEnum.Symbol;
            if (typeof(T) == typeof(UUID)) return TypeEnum.UUID;

            if (typeof(T) == typeof(Variant)) return TypeEnum.Variant;
            if (typeof(T) == typeof(Json)) return TypeEnum.Json;
            if (typeof(T) == typeof(string)) return TypeEnum.String;
            if (typeof(T) == typeof(ErrorCode)) return TypeEnum.ErrorCode;
            if (typeof(T).IsArray) return TypeEnum.Array;

            if (typeof(T) == typeof(Table)) return TypeEnum.Table;
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Matrix<>)) return TypeEnum.Matrix;
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>)) return TypeEnum.TaggedKeyValue;
            // if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ValueWithTimestamp<>)) return TypeEnum.ValueWithTimestamp;

            // TODO known types, otherwise will fallback to fixed binary

            // TypeEnum is for known types only
            // TODO Attrobute KnownType and 
            //#pragma warning disable 618
            //            if (TypeHelper<T>.Size >= 0) return TypeEnum.FixedBinary;
            //#pragma warning restore 618

            // TODO TypeEnum.Object is for Object with known subtype (runtime/app specific) - it is like a container 
            // but for a single object
            // return TypeEnum.Object;

            return TypeEnum.None;
        }

        // ReSharper disable once StaticMemberInGenericType
        private static int _elementType = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetElementTypeEnum()
        {
            if (_elementType != -1)
            {
                return (TypeEnum)(byte)_elementType;
            }
            var ty = typeof(T);
            if (ty.IsArray)
            {
                var elTy = ty.GetElementType();
                var elTypeEnum = VariantHelper.GetTypeEnum(elTy);
                _elementType = (int)elTypeEnum;
                return elTypeEnum;
            }
            return TypeEnum.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetElementTypeSize()
        {
            var ty = typeof(T);
            if (!ty.IsArray)
            {
                ThrowHelper.ThrowInvalidOperationException_ForVariantTypeMissmatch();
            }
            var elTy = ty.GetElementType();
#pragma warning disable 618
            return TypeHelper.GetSize(elTy);
#pragma warning restore 618
        }
    }

    public class VariantHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetTypeEnum(Type ty)
        {
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
            //if (ty == typeof(Money)) return TypeEnum.Money;
            if (ty == typeof(DateTime)) return TypeEnum.DateTime;
            if (ty == typeof(Timestamp)) return TypeEnum.Timestamp;

            if (ty == typeof(Id)) return TypeEnum.Id;
            if (ty == typeof(Symbol)) return TypeEnum.Symbol;
            if (ty == typeof(UUID)) return TypeEnum.UUID;

            if (ty == typeof(Variant)) return TypeEnum.Variant;
            if (ty == typeof(string)) return TypeEnum.String;
            if (ty == typeof(ErrorCode)) return TypeEnum.ErrorCode;

            if (ty.IsArray) return TypeEnum.Array;
            if (ty == typeof(Matrix<>)) return TypeEnum.Matrix;
            if (ty == typeof(Table)) return TypeEnum.Table;

            // TODO known types, otherwise will fallback to fixed binary

#pragma warning disable 618
            if (TypeHelper.GetSize(ty) > 0) return TypeEnum.FixedBinary;
#pragma warning restore 618

            //if (typeof(Exception).IsAssignableFrom(ty)) return TypeEnum.ErrorCode;

            return TypeEnum.Object;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Type GetType(TypeEnum typeEnum, TypeEnum subTypeEnum = TypeEnum.None)
        {
            if (typeEnum == TypeEnum.Variant)
            {
                return typeof(Variant);
            }

            if ((int)typeEnum < Variant.KnownSmallTypesLimit)
            {
                switch (typeEnum)
                {
                    case TypeEnum.Bool:
                        return typeof(bool);

                    case TypeEnum.Int8:
                        return typeof(sbyte);

                    case TypeEnum.Int16:
                        return typeof(short);

                    case TypeEnum.Int32:
                        return typeof(int);

                    case TypeEnum.Int64:
                        return typeof(long);

                    case TypeEnum.UInt8:
                        return typeof(byte);

                    case TypeEnum.UInt16:
                        return typeof(ushort);

                    case TypeEnum.UInt32:
                        return typeof(uint);

                    case TypeEnum.UInt64:
                        return typeof(ulong);

                    case TypeEnum.Float32:
                        return typeof(float);

                    case TypeEnum.Float64:
                        return typeof(double);

                    case TypeEnum.Decimal:
                        return typeof(decimal);

                    case TypeEnum.Price:
                        return typeof(Price);

                    case TypeEnum.Money:
                        throw new NotImplementedException();
                    //return typeof(Money);

                    case TypeEnum.DateTime:
                        return typeof(DateTime);

                    case TypeEnum.Timestamp:
                        return typeof(Timestamp);

                    case TypeEnum.Id:
                        return typeof(Id);

                    case TypeEnum.Symbol:
                        return typeof(Symbol);

                    case TypeEnum.UUID:
                        return typeof(UUID);

                    case TypeEnum.ErrorCode:
                        return typeof(ErrorCode);

                    case TypeEnum.Date:
                        throw new NotImplementedException();
                    case TypeEnum.Time:
                        throw new NotImplementedException();
                    case TypeEnum.Complex32:
                        throw new NotImplementedException();
                    case TypeEnum.Complex64:
                        throw new NotImplementedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (typeEnum == TypeEnum.Array)
            {
                var elementType = GetType(subTypeEnum);
                return elementType.MakeArrayType();
            }

            if (typeEnum == TypeEnum.Matrix)
            {
                var elementType = GetType(subTypeEnum);
                return typeof(Table).MakeGenericType(elementType);
            }

            if (typeEnum == TypeEnum.Table)
            {
                return typeof(Table);
            }

            if (typeEnum == TypeEnum.String)
            {
                return typeof(string);
            }

            //if (typeEnum == TypeEnum.ErrorCode)
            //{
            //    return typeof(Exception);
            //}

            throw new NotImplementedException();
        }
    }
}