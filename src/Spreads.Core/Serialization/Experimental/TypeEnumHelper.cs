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
        public static readonly short* Sizes = InitSizes();

        private static short* InitSizes()
        {
            var allocSize = SizeOf<short>() * 256 + 64;
            // Never free until process dies.
            var ptr = Marshal.AllocHGlobal(allocSize);
            (new Span<byte>((void*)ptr, allocSize)).Clear();

            var alignedPtr = (IntPtr)BitUtil.Align((long)ptr, 64);

            var sizes = (short*)alignedPtr;

            #region Known fixed-size scalar types

            // ReSharper disable once PossibleNullReferenceException
            sizes[(byte)TypeEnumEx.None] = 0;

            sizes[(byte)TypeEnumEx.Int8] = (short)SizeOf<sbyte>();
            sizes[(byte)TypeEnumEx.Int16] = (short)SizeOf<short>();
            sizes[(byte)TypeEnumEx.Int32] = (short)SizeOf<int>();
            sizes[(byte)TypeEnumEx.Int64] = (short)SizeOf<long>();
            sizes[(byte)TypeEnumEx.Int128] = 16;

            sizes[(byte)TypeEnumEx.UInt8] = (short)SizeOf<byte>();
            sizes[(byte)TypeEnumEx.UInt16] = (short)SizeOf<ushort>();
            sizes[(byte)TypeEnumEx.UInt32] = (short)SizeOf<uint>();
            sizes[(byte)TypeEnumEx.UInt64] = (short)SizeOf<ulong>();
            sizes[(byte)TypeEnumEx.UInt128] = 16;

            sizes[(byte)TypeEnumEx.Float16] = 2;
            sizes[(byte)TypeEnumEx.Float32] = (short)SizeOf<float>();
            sizes[(byte)TypeEnumEx.Float64] = (short)SizeOf<double>();
            sizes[(byte)TypeEnumEx.Float128] = 16;

            sizes[(byte)TypeEnumEx.Decimal32] = 4;
            sizes[(byte)TypeEnumEx.Decimal64] = 8;
            sizes[(byte)TypeEnumEx.Decimal128] = 16;

            sizes[(byte)TypeEnumEx.Decimal] = 16;
            sizes[(byte)TypeEnumEx.SmallDecimal] = SmallDecimal.Size;

            sizes[(byte)TypeEnumEx.Bool] = 1;
            sizes[(byte)TypeEnumEx.Utf16Char] = 2;
            sizes[(byte)TypeEnumEx.UUID] = 16;

            sizes[(byte)TypeEnumEx.DateTime] = 8;
            sizes[(byte)TypeEnumEx.Timestamp] = Timestamp.Size;

            sizes[(byte)TypeEnumEx.Symbol] = Symbol.Size;
            sizes[(byte)TypeEnumEx.Symbol32] = Symbol32.Size;
            sizes[(byte)TypeEnumEx.Symbol64] = Symbol64.Size;
            sizes[(byte)TypeEnumEx.Symbol128] = Symbol128.Size;
            sizes[(byte)TypeEnumEx.Symbol256] = Symbol256.Size;

            #endregion Known fixed-size scalar types

            for (int i = 1; i <= 31; i++)
            {
                if (unchecked((uint)sizes[i]) > 16)
                {
                    ThrowHelper.FailFast($"Sizes[{i}] == {sizes[i]} > 16");
                }
            }

            for (int i = 64; i <= 127; i++)
            {
                sizes[i] = -1;
            }

            for (int i = 1; i <= TypeEnumOrFixedSize.MaxFixedSize; i++)
            {
                sizes[TypeEnumOrFixedSize.MaxTypeEnum + i] = (byte)(i);
            }

            return sizes;
        }

        /// <summary>
        /// Returns a positive size of top-level <see cref="TypeEnumEx"/>
        /// for scalars or -1 for composite types, which could still be
        /// fixed size. Use <see cref="DataTypeHeaderEx.Size"/> property
        /// to get the size of a composite type.
        /// </summary>
        /// <param name="typeEnumValue"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short GetSize(byte typeEnumValue)
        {
            #region Branchless

            // Branchless is 3-5x slower and we pay the cost always.
            // Our goal is not to pay anything for known scalars
            // and the first 32 of them fit 1 cache line.
            // For other types we are OK with L1 miss or branch mis-prediction,
            // but only full lookup table does not impact known scalars.
            // L1 miss is less or ~same as wrong branch.

            // Interesting technique if we could often have L2 misses, but here it is slower than lookup.

            //var localSized = stackalloc int[4];
            //var val7Bit = (typeEnumValue & 0b_0111_1111) * (typeEnumValue >> 7);
            //// 00 - known scalar type
            //localSized[0] = _sizes[typeEnumValue & 0b_0011_1111]; // in L1
            //// 01 - var size or container
            //localSized[1] = -1;
            //// 10 - fixed size < 65
            //localSized[2] = (short)val7Bit;
            //// 11 fixed size [65;128]
            //localSized[3] = (short)val7Bit;
            //var localIdx = (typeEnumValue & 0b_1100_0000) >> 6;
            //return localSized[localIdx];

            #endregion Branchless

            return Sizes[typeEnumValue];
        }
    }

    /// <summary>
    /// Note that performance here is the last concern and a static caching must be used after the first call.
    /// </summary>
    internal static class ReflectionHelper
    {
        public static object Create(Type type)
        {
            var method = typeof(ArraySerializerFactory).GetTypeInfo().GetMethod("GenericCreate");
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

        public static Func<TResult> MakeFunc<TTy, TResult>(string methodName, Type genericType)
        {
            var method = typeof(TTy).GetTypeInfo().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase);
            var genericMethod = method?.MakeGenericMethod(genericType);

            var methodDelegate = Delegate.CreateDelegate(typeof(Func<TResult>), null, genericMethod);

            return methodDelegate as Func<TResult>;
        }

        public static Func<TResult> MakeFunc<TTy, TResult>(string methodName, Type genericType1, Type genericType2)
        {
            var method = typeof(TTy).GetTypeInfo().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase);
            var genericMethod = method?.MakeGenericMethod(genericType1, genericType2);

            var methodDelegate = Delegate.CreateDelegate(typeof(Func<TResult>), null, genericMethod);

            return methodDelegate as Func<TResult>;
        }

        // Not used:

        //public static Func<T1, TResult> MakeFunc<TTy, T1, TResult>(string methodName)
        //{
        //    var method = typeof(TTy).GetTypeInfo().GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.IgnoreCase);
        //    var genericMethod = method?.MakeGenericMethod(typeof(T1));

        //    // var genericFuncType = typeof(Func<T1,TResult>).MakeGenericType(typeof(T1), typeof(TResult));
        //    var methodDelegate = Delegate.CreateDelegate(typeof(Func<T1, TResult>), null, genericMethod);

        //    return methodDelegate as Func<T1, TResult>;
        //}

        //public static Func<T1, TResult> MakeFunc<TTy, T1, TResult>(TTy instance, string methodName)
        //{
        //    var method = typeof(TTy).GetTypeInfo().GetMethod(methodName);
        //    var genericMethod = method?.MakeGenericMethod(typeof(T1));

        //    var genericFuncType = typeof(Func<,,>).MakeGenericType(typeof(TTy), typeof(T1), typeof(TResult));
        //    var methodDelegate = Delegate.CreateDelegate(genericFuncType, instance, genericMethod);

        //    return methodDelegate as Func<T1, TResult>;
        //}
    }

    internal struct TypeInfo<T>
    {
        public DataTypeHeaderEx Header;

        /// <summary>
        /// Calculated recursively, not just pinned size/
        /// </summary>
        public short FixedSize;

        /// <summary>
        /// Sequence of TEOFS elements that are required to fully describe
        /// a type *in addition* to <see cref="Header"/>.
        /// </summary>
        public List<TypeEnumOrFixedSize> Composite;
    }

    // ReSharper disable once ConvertToStaticClass
    internal sealed unsafe class TypeEnumHelper<T>
    {
        private TypeEnumHelper()
        {
        }

        // TypeHelper`T does a first-pass, parses BinarySerializationAttribute,
        // checks if the type implements IBinarySerializerEx or registers known ones,
        // calculates pinned size, etc.
        // But TypeHelper`T is dumb and straightforward,
        // it is only concerned about the type T as is.
        // Here we handle special cases, e.g. redirecting special type
        // shapes to custom serializers, calculate composite fixed size
        // and construct composite type schema.

        // ReSharper disable StaticMemberInGenericType
        public static readonly TypeInfo<T> TypeInfo = CreateTypeInfo();

        public static readonly DataTypeHeaderEx DataTypeHeader = TypeInfo.Header;

        public static readonly short FixedSize = TypeInfo.FixedSize;
        public static readonly bool IsFixedSize = FixedSize > 0;

        internal static readonly DataTypeHeaderEx DefaultBinaryHeader = new DataTypeHeaderEx
        {
            VersionAndFlags =
            {
                ConverterVersion = 0,
                IsBinary = true,
                CompressionMethod = CompressionMethod.None
            },
            TEOFS = DataTypeHeader.TEOFS,
            TEOFS1 = DataTypeHeader.TEOFS1,
            TEOFS2 = DataTypeHeader.TEOFS2
        };

        internal static readonly DataTypeHeaderEx DefaultBinaryHeaderWithTs = new DataTypeHeaderEx
        {
            VersionAndFlags =
            {
                ConverterVersion = 0,
                IsBinary = true,
                CompressionMethod = CompressionMethod.None,
                IsTimestamped = true
            },
            TEOFS = DataTypeHeader.TEOFS,
            TEOFS1 = DataTypeHeader.TEOFS1,
            TEOFS2 = DataTypeHeader.TEOFS2
        };

        internal static TypeInfo<T> CreateTypeInfo()
        {
            var teofs = GetTeofs();
            var te = teofs.TypeEnum;
            if ((byte)te <= TypeEnumOrFixedSize.MaxScalarEnum
                || te == TypeEnumEx.FixedSize)
            {
                return new TypeInfo<T>
                {
                    // Scalars have only one field set
                    Header = new DataTypeHeaderEx
                    {
                        TEOFS = teofs
                    },
                    FixedSize = teofs.Size > 0 ? teofs.Size : TypeHelper<T>.PinnedSize
                };
            }

            if (te == TypeEnumEx.Array)
            {
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeaderEx>(
                    nameof(CreateArrayInfo),
                    typeof(T).GetElementType()
                );
                var header = func();
                return new TypeInfo<T>()
                {
                    Header = header,
                    FixedSize = -1
                };
            }

            if (te == TypeEnumEx.TupleTN)
            {
                // Here we need to leave count slot at zero and as a special case write it from BS

                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeaderEx>(
                    nameof(CreateFixedArrayTHeader),
                    typeof(T).GetGenericArguments()[0]
                );
                var header = func();
                return new TypeInfo<T>
                {
                    Header = header,
                    FixedSize = 0 // This is a nasty special case and is rewritten by BS
                };
            }

            if (te == TypeEnumEx.TupleT2
                || te == TypeEnumEx.TupleT3
                || te == TypeEnumEx.TupleT4
                || te == TypeEnumEx.TupleT5
                || te == TypeEnumEx.TupleT6
            )
            {
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeaderEx>(
                    nameof(CreateFixedTupleTHeader),
                    typeof(T).GetGenericArguments()[0]
                );
                var header = func();
                var tbs = TypeHelper<T>.BinarySerializerEx;
                var fs = tbs?.FixedSize ?? -1;
                return new TypeInfo<T>
                {
                    Header = header,
                    FixedSize = fs
                };
            }

            if (te == TypeEnumEx.Tuple2
                || te == TypeEnumEx.Tuple2Byte
                || te == TypeEnumEx.Tuple2Long
            )
            {
                var gArgs = typeof(T).GetGenericArguments();
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeaderEx>(
                    nameof(CreateTuple2Header),
                    gArgs[0], gArgs[1]
                );
                var header = func();
                var tbs = TypeHelper<T>.BinarySerializerEx;
                var fs = tbs?.FixedSize ?? -1;
                return new TypeInfo<T>
                {
                    Header = header,
                    FixedSize = fs
                };
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Recursively get elemnt type info, fuse jagged arrays.
        /// </summary>
        private static DataTypeHeaderEx CreateArrayInfo<TElement>()
        {
            var elHeader = TypeEnumHelper<TElement>.DataTypeHeader;

            if (elHeader.TEOFS.TypeEnum == TypeEnumEx.JaggedArray)
            {
                // Fuse jagged arrays.
                return new DataTypeHeaderEx
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.JaggedArray),
                    TEOFS1 = elHeader.TEOFS1,
                    TEOFS2 = new TypeEnumOrFixedSize((byte)(elHeader.TEOFS2.Size + 1)), // JA depth
                };
            }

            if (elHeader.TEOFS.TypeEnum == TypeEnumEx.Array)
            {
                if (elHeader.TEOFS2 != default)
                {
                    elHeader.TEOFS1 = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType);
                    elHeader.TEOFS2 = default;
                }
                return new DataTypeHeaderEx
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.JaggedArray),
                    TEOFS1 = elHeader.TEOFS1,
                    TEOFS2 = new TypeEnumOrFixedSize(1)
                };
            }

            if (TypeEnumHelper<TElement>.DataTypeHeader.TEOFS2 != default)
            {
                // Too many nested levels. We keep info that this is an array
                // but child element must have schema defined.
                // TODO need to create schema and cache it.
                // TODO schema should be top-level only
                return new DataTypeHeaderEx
                {
                    TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                    TEOFS1 = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType),
                };
            }
            return new DataTypeHeaderEx
            {
                TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                TEOFS1 = elHeader.TEOFS,
                TEOFS2 = elHeader.TEOFS1
            };
        }

        private static DataTypeHeaderEx CreateFixedArrayTHeader<T1>()
        {
            Debug.Assert(GetTeofs().TypeEnum == TypeEnumEx.TupleTN);
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;

            if (!t1Header.IsScalar)
            {
                return new DataTypeHeaderEx
                {
                    TEOFS = GetTeofs(), // Parent
                    TupleNCount = default,
                    TupleTNTeofs = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType)
                    
                };
            }
            return new DataTypeHeaderEx
            {
                TEOFS = GetTeofs(),
                TEOFS1 = default, // Must be overwritten by BS
                TEOFS2 = t1Header.TEOFS1
            };
        }

        private static DataTypeHeaderEx CreateFixedTupleTHeader<T1>()
        {
            Debug.Assert(GetTeofs().TypeEnum == TypeEnumEx.TupleT2);
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;

            // Note: TEOFS2 not IsScalar, fixed tuples could contain types that require 2 slots, e.g. Tuple2<Tuple2,Tuple2> or Tuple2<int[],int[]> TODO test those
            if (t1Header.TEOFS2 != default)
            {
                return new DataTypeHeaderEx()
                {
                    TEOFS = GetTeofs(), // Parent
                    TEOFS1 = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType)
                };
            }
            return new DataTypeHeaderEx
            {
                TEOFS = GetTeofs(),
                TEOFS1 = t1Header.TEOFS,
                TEOFS2 = t1Header.TEOFS1
            };
        }

        private static DataTypeHeaderEx CreateTuple2Header<T1, T2>()
        {
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;
            if (!t1Header.IsScalar)
            {
                t1Header.TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType);
            }

            var t2Header = TypeEnumHelper<T2>.DataTypeHeader;
            if (!t2Header.IsScalar)
            {
                t2Header.TEOFS = new TypeEnumOrFixedSize(TypeEnumEx.CompositeType);
                t2Header.TEOFS1 = default;
            }

            return new DataTypeHeaderEx
            {
                TEOFS = GetTeofs(),
                TEOFS1 = t1Header.TEOFS,
                TEOFS2 = t2Header.TEOFS
            };
        }

        /// <summary>
        /// Top-level <see cref="TypeEnumOrFixedSize"/>.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeEnumOrFixedSize GetTeofs()
        {
            var topTe = GetTypeEnum();
            if (topTe == TypeEnumEx.FixedSize)
            {
                var fs = checked((ushort)TypeHelper<T>.FixedSize);
                if (fs <= 128)
                {
                    return new TypeEnumOrFixedSize((byte)fs);
                }
            }

            return new TypeEnumOrFixedSize(topTe);
        }

        /// <summary>
        /// Determines top-level shape of a type.
        /// TODO Decides priority of custom serializers over blittable representation
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
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>))
            {
                return TypeEnumEx.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT2;
                }
                return TypeEnumEx.Tuple2;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                (typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,>)
                 ||
                 typeof(T).GetGenericTypeDefinition() == typeof(Tuple<,>)
                )
               )
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT2;
                }
                return TypeEnumEx.Tuple2;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT3;
                }
                return TypeEnumEx.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT4;
                }
                return TypeEnumEx.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT5;
                }
                return TypeEnumEx.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnumEx.TupleT6;
                }
                return TypeEnumEx.TupleTN;
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
                    return TypeEnumEx.TupleTN;
                }
                return TypeEnumEx.TupleTN;
            }

            // TODO Tuple2Version, KeyIndexValue
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>)) return TypeEnumEx.Tuple2Byte;

            #endregion Tuple-like

            // TODO(?) Memory manager, RetainableMemory, however they have Memory field and we are serializing *Memory*, not a container, in that case.
            if (typeof(T).IsArray) return TypeEnumEx.Array;
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Memory<>)) { return TypeEnumEx.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(RetainedMemory<>)) { return TypeEnumEx.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(VectorStorage<>)) { return TypeEnumEx.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ArrayWrapper<>)) { return TypeEnumEx.Array; }

            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.Matrix<>)) return TypeEnumEx.NDArray;
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.NDArray<>)) return TypeEnumEx.NDArray;

            // if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ValueWithTimestamp<>)) return TypeEnumEx.ValueWithTimestamp;

            if (typeof(T) == typeof(Table)) return TypeEnumEx.Table;

            if (typeof(T) == typeof(Variant)) return TypeEnumEx.Variant;

            // Order of the three following checks is important.
            // Fixed types should rarely have a custom serializer
            // but if they then the serializer is more important.

            if (TypeHelper<T>.HasBinarySerializer) return TypeEnumEx.UserType;

            if (TypeHelper<T>.FixedSize > 0)
            {
                return TypeEnumEx.FixedSize;
            }

            return TypeEnumEx.UserType;
        }
    }
}
