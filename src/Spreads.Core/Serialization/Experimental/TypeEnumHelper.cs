// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections.Internal;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization.Experimental
{
    internal static unsafe class TypeEnumHelper
    {
        public static readonly short* Sizes; // = new short[256];

        static TypeEnumHelper()
        {
            var allocSize = SizeOf<short>() * 256 + 64;
            // Never free until process dies.
            var ptr = Marshal.AllocHGlobal(allocSize);
            (new Span<byte>((void*)ptr, allocSize)).Clear();

            var alignedPtr = (IntPtr)BitUtil.Align((long)ptr, 64);

            Sizes = (short*)alignedPtr;

            #region Known fixed-size scalar types

            // ReSharper disable once PossibleNullReferenceException
            Sizes[(byte)TypeEnumEx.None] = 0;

            Sizes[(byte)TypeEnumEx.Int8] = (short)SizeOf<sbyte>();
            Sizes[(byte)TypeEnumEx.Int16] = (short)SizeOf<short>();
            Sizes[(byte)TypeEnumEx.Int32] = (short)SizeOf<int>();
            Sizes[(byte)TypeEnumEx.Int64] = (short)SizeOf<long>();
            Sizes[(byte)TypeEnumEx.Int128] = 16;

            Sizes[(byte)TypeEnumEx.UInt8] = (short)SizeOf<byte>();
            Sizes[(byte)TypeEnumEx.UInt16] = (short)SizeOf<ushort>();
            Sizes[(byte)TypeEnumEx.UInt32] = (short)SizeOf<uint>();
            Sizes[(byte)TypeEnumEx.UInt64] = (short)SizeOf<ulong>();
            Sizes[(byte)TypeEnumEx.UInt128] = 16;

            Sizes[(byte)TypeEnumEx.Float16] = 2;
            Sizes[(byte)TypeEnumEx.Float32] = (short)SizeOf<float>();
            Sizes[(byte)TypeEnumEx.Float64] = (short)SizeOf<double>();
            Sizes[(byte)TypeEnumEx.Float128] = 16;

            Sizes[(byte)TypeEnumEx.Decimal32] = 4;
            Sizes[(byte)TypeEnumEx.Decimal64] = 8;
            Sizes[(byte)TypeEnumEx.Decimal128] = 16;

            Sizes[(byte)TypeEnumEx.Decimal] = 16;
            Sizes[(byte)TypeEnumEx.SmallDecimal] = SmallDecimal.Size;

            Sizes[(byte)TypeEnumEx.Bool] = 1;
            Sizes[(byte)TypeEnumEx.Utf16Char] = 2;
            Sizes[(byte)TypeEnumEx.UUID] = 16;

            Sizes[(byte)TypeEnumEx.DateTime] = 8;
            Sizes[(byte)TypeEnumEx.Timestamp] = Timestamp.Size;

            Sizes[(byte)TypeEnumEx.Symbol] = Symbol.Size;
            Sizes[(byte)TypeEnumEx.Symbol32] = Symbol32.Size;
            Sizes[(byte)TypeEnumEx.Symbol64] = Symbol64.Size;
            Sizes[(byte)TypeEnumEx.Symbol128] = Symbol128.Size;
            Sizes[(byte)TypeEnumEx.Symbol256] = Symbol256.Size;

            #endregion Known fixed-size scalar types

            for (int i = 1; i <= 31; i++)
            {
                if (unchecked((uint)Sizes[i]) > 16)
                {
                    ThrowHelper.FailFast($"Sizes[{i}] == {Sizes[i]} > 16");
                }
            }

            for (int i = 64; i <= 127; i++)
            {
                Sizes[i] = -1;
            }

            for (int i = 1; i <= TypeEnumOrFixedSize.MaxFixedSize; i++)
            {
                Sizes[TypeEnumOrFixedSize.MaxTypeEnum + i] = (byte)(i);
            }
        }
    }

    internal static class ReflectionHelper
    {
        public static object Create(Type type)
        {
            var method = typeof(ArrayConverterFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }

        public static bool SameGenericArgs<T>()
        {
            if (!typeof(T).IsGenericType)
            {
                throw new InvalidOperationException();
            }

            var args = typeof(T).GetGenericArguments();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != args[0])
                {
                    return false;
                }
            }

            return true;
        }

        public static Func<TResult> Func<TTy, TResult>(string methodName, Type genericType)
        {
            var method = typeof(TTy).GetTypeInfo().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase);
            var genericMethod = method?.MakeGenericMethod(genericType);

            var methodDelegate = Delegate.CreateDelegate(typeof(Func<TResult>), null, genericMethod);

            return methodDelegate as Func<TResult>;
        }

        public static Func<T1, TResult> Func<TTy, T1, TResult>(string methodName)
        {
            var method = typeof(TTy).GetTypeInfo().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase);
            var genericMethod = method?.MakeGenericMethod(typeof(T1));

            // var genericFuncType = typeof(Func<T1,TResult>).MakeGenericType(typeof(T1), typeof(TResult));
            var methodDelegate = Delegate.CreateDelegate(typeof(Func<T1, TResult>), null, genericMethod);

            return methodDelegate as Func<T1, TResult>;
        }

        public static Func<T1, TResult> Func<TTy, T1, TResult>(TTy instance, string methodName)
        {
            var method = typeof(TTy).GetTypeInfo().GetMethod(methodName);
            var genericMethod = method?.MakeGenericMethod(typeof(T1));

            var genericFuncType = typeof(Func<,,>).MakeGenericType(typeof(TTy), typeof(T1), typeof(TResult));
            var methodDelegate = Delegate.CreateDelegate(genericFuncType, instance, genericMethod);

            return methodDelegate as Func<T1, TResult>;
        }
    }

    // ReSharper disable once ConvertToStaticClass
    internal sealed unsafe class TypeEnumHelper<T>
    {
        private TypeEnumHelper()
        {
        }

        // Cache VariantHeader
        // ReSharper disable once StaticMemberInGenericType
        public static readonly VariantHeader VariantHeader = CreateVariantHeader();

        internal static VariantHeader CreateVariantHeader()
        {
            // var topTe = GetTypeEnum();
            var teofs = GetTeofs();
            var te = teofs.TypeEnum;
            if ((byte)te <= TypeEnumOrFixedSize.MaxScalarEnum
                || te == TypeEnumEx.FixedSize)
            {
                // Scalars have only one field set
                return new VariantHeader
                {
                    TEOFS = teofs
                };
            }

            if (te == TypeEnumEx.Array)
            {
                var func = ReflectionHelper.Func<TypeEnumHelper<T>, VariantHeader>(
                    nameof(CreateArrayHeader),
                    typeof(T).GetElementType()
                    );
                var header = func();
                return header;
            }

            if (te == TypeEnumEx.Tuple2T)
            {
                var func = ReflectionHelper.Func<TypeEnumHelper<T>, VariantHeader>(
                    nameof(CreateTuple2THeader),
                    typeof(T).GetGenericArguments()[0]
                );
                var header = func();
                return header;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Recursively get elemnt type info, fuse jagged arrays.
        /// </summary>
        private static VariantHeader CreateArrayHeader<TElement>()
        {
            var elHeader = TypeEnumHelper<TElement>.VariantHeader;
            if (elHeader.TEOFS.TypeEnum == TypeEnumEx.JaggedArray)
            {
                return new VariantHeader()
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.JaggedArray),
                    TEOFS1 = new TypeEnumOrFixedSize((byte)(elHeader.TEOFS1.Size + 1)), // JA depth
                    TEOFS2 = elHeader.TEOFS2,
                    TEOFS3 = elHeader.TEOFS3
                };
            }

            if (elHeader.TEOFS.TypeEnum == TypeEnumEx.Array)
            {
                return new VariantHeader()
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.JaggedArray),
                    TEOFS1 = new TypeEnumOrFixedSize(1),
                    TEOFS2 = elHeader.TEOFS1,
                    TEOFS3 = elHeader.TEOFS2,
                };
            }

            if (TypeEnumHelper<TElement>.VariantHeader.TEOFS3 != default)
            {
                // Too many nested levels. We keep info that this is an array
                // but child element must have schema defined.
                // TODO need to create schema and cache it.
                // TODO schema should be top-level only
                return new VariantHeader()
                {
                    TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                    TEOFS1 = new TypeEnumOrFixedSize(TypeEnumEx.Schema),
                };
            }
            return new VariantHeader
            {
                TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                TEOFS1 = elHeader.TEOFS,
                TEOFS2 = elHeader.TEOFS1,
                TEOFS3 = elHeader.TEOFS2,
            };
        }

        private static VariantHeader CreateTuple2THeader<T1>()
        {
            Debug.Assert(TypeEnumHelper<T>.GetTeofs().TypeEnum == TypeEnumEx.Tuple2T);
            return new VariantHeader
            {
                TEOFS = GetTeofs(),
                TEOFS1 = TypeEnumHelper<T1>.GetTeofs()
            };
        }

        private static VariantHeader CreateVariantHeader<TA>(ref TA[] _)
        {
            var t = typeof(T);
            Console.WriteLine(t.Name);
            return default;
        }

        private static VariantHeader CreateVariantHeader<T1>(ValueTuple<T1, T1> _)
        {
            return default;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private static VariantHeader CreateVariantHeader<T1>((T1, T1, T1) _)
        //{
        //    return default;
        //}

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //private static TypeEnumEx GetArrayHeader<TA>(TA[] _)
        //{
        //    return default;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeEnumOrFixedSize GetTeofs()
        {
            var topTe = GetTypeEnum();
            if (topTe == TypeEnumEx.FixedSize)
            {
                return new TypeEnumOrFixedSize((byte)TypeHelper<T>.FixedSize);
            }

            return new TypeEnumOrFixedSize(topTe);
        }

        /// <summary>
        /// TODO this is only for top-level type, should not touch
        /// static fields for primitives
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeEnumEx GetTypeEnum()
        {
            if (typeof(T) == typeof(bool)) return TypeEnumEx.Bool;
            if (typeof(T) == typeof(byte)) return TypeEnumEx.UInt8;
            if (typeof(T) == typeof(char)) return TypeEnumEx.Utf16Char;
            if (typeof(T) == typeof(sbyte)) return TypeEnumEx.Int8;
            if (typeof(T) == typeof(short)) return TypeEnumEx.Int16;
            if (typeof(T) == typeof(ushort)) return TypeEnumEx.UInt16;
            if (typeof(T) == typeof(int)) return TypeEnumEx.Int32;
            if (typeof(T) == typeof(uint)) return TypeEnumEx.UInt32;
            if (typeof(T) == typeof(long)) return TypeEnumEx.Int64;
            if (typeof(T) == typeof(ulong)) return TypeEnumEx.UInt64;
            if (typeof(T) == typeof(IntPtr)) return TypeEnumEx.Int64;
            if (typeof(T) == typeof(UIntPtr)) return TypeEnumEx.UInt64;
            if (typeof(T) == typeof(float)) return TypeEnumEx.Float32;
            if (typeof(T) == typeof(double)) return TypeEnumEx.Float64;
            if (typeof(T) == typeof(decimal)) return TypeEnumEx.Decimal;
            if (typeof(T) == typeof(SmallDecimal)) return TypeEnumEx.SmallDecimal;

            if (typeof(T) == typeof(UUID)) return TypeEnumEx.UUID;

            if (typeof(T) == typeof(DateTime)) return TypeEnumEx.DateTime;
            if (typeof(T) == typeof(Timestamp)) return TypeEnumEx.Timestamp;

            if (typeof(T) == typeof(Symbol)) return TypeEnumEx.Symbol;
            if (typeof(T) == typeof(Symbol32)) return TypeEnumEx.Symbol32;
            if (typeof(T) == typeof(Symbol64)) return TypeEnumEx.Symbol64;
            if (typeof(T) == typeof(Symbol128)) return TypeEnumEx.Symbol128;
            if (typeof(T) == typeof(Symbol256)) return TypeEnumEx.Symbol256;

            if (typeof(T) == typeof(byte[])) return TypeEnumEx.Binary;
            if (typeof(T) == typeof(Memory<byte>)) return TypeEnumEx.Binary;
            if (typeof(T) == typeof(DirectBuffer)) return TypeEnumEx.Binary;
            if (typeof(T) == typeof(RetainedMemory<byte>)) return TypeEnumEx.Binary;

            if (typeof(T) == typeof(string)) return TypeEnumEx.Utf16String;

            if (typeof(T) == typeof(Json)) return TypeEnumEx.Json;

            #region Tuple-like

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>)) { return TypeEnumEx.FixedArrayT; }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple2T;
                }
                return TypeEnumEx.Tuple2;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple2T;
                }
                return TypeEnumEx.Tuple2;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple3T;
                }
                return TypeEnumEx.Tuple3;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple4T;
                }
                return TypeEnumEx.Schema;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple5T;
                }
                return TypeEnumEx.Schema;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.Tuple6T;
                }
                return TypeEnumEx.Schema;
            }

            //
            if (typeof(T).GetTypeInfo().IsGenericType &&
                (typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,>)
                 ||
                 typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,,>))
                )
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.FixedArrayT;
                }
                return TypeEnumEx.Schema;
            }

            // TODO Tuple2Version, KeyIndexValue
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>)) return TypeEnumEx.Tuple2Tag;

            #endregion Tuple-like

            // TODO(?) Memory manager, RetainableMemory, however they have Memory field and we are serializing *Memory*, not a container, in that case.
            if (typeof(T).IsArray) return TypeEnumEx.Array;
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Memory<>)) { return TypeEnumEx.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(RetainedMemory<>)) { return TypeEnumEx.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(VectorStorage<>)) { return TypeEnumEx.Array; }

            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.Matrix<>)) return TypeEnumEx.NDArray;
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.NDArray<>)) return TypeEnumEx.NDArray;

            // if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ValueWithTimestamp<>)) return TypeEnumEx.ValueWithTimestamp;

            if (typeof(T) == typeof(Table)) return TypeEnumEx.Table;

            if (typeof(T) == typeof(Variant)) return TypeEnumEx.Variant;

            // TODO Schema

            if (TypeHelper<T>.HasBinarySerializer) return TypeEnumEx.UserKnownType;
            // order between the two! fixed types should rarely have custom serializer but if they do it is more important.
            if (TypeHelper<T>.FixedSize >= 0) return TypeEnumEx.FixedSize;

            return TypeEnumEx.UserType;
        }
    }
}