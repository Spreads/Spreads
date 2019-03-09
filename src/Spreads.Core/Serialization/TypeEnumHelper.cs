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

namespace Spreads.Serialization
{
    internal static unsafe class TypeEnumHelper
    {
        public static readonly short* Sizes = InitSizes();

        private static short* InitSizes()
        {
            var allocSize = Unsafe.SizeOf<short>() * 256 + 64;
            // Never free until process dies.
            var ptr = Marshal.AllocHGlobal(allocSize);
            (new Span<byte>((void*)ptr, allocSize)).Clear();

            var alignedPtr = (IntPtr)BitUtil.Align((long)ptr, 64);

            var sizes = (short*)alignedPtr;

            #region Known fixed-size scalar types

            // ReSharper disable once PossibleNullReferenceException
            sizes[(byte)TypeEnum.None] = 0;

            sizes[(byte)TypeEnum.Int8] = (short)Unsafe.SizeOf<sbyte>();
            sizes[(byte)TypeEnum.Int16] = (short)Unsafe.SizeOf<short>();
            sizes[(byte)TypeEnum.Int32] = (short)Unsafe.SizeOf<int>();
            sizes[(byte)TypeEnum.Int64] = (short)Unsafe.SizeOf<long>();
            sizes[(byte)TypeEnum.Int128] = 16;

            sizes[(byte)TypeEnum.UInt8] = (short)Unsafe.SizeOf<byte>();
            sizes[(byte)TypeEnum.UInt16] = (short)Unsafe.SizeOf<ushort>();
            sizes[(byte)TypeEnum.UInt32] = (short)Unsafe.SizeOf<uint>();
            sizes[(byte)TypeEnum.UInt64] = (short)Unsafe.SizeOf<ulong>();
            sizes[(byte)TypeEnum.UInt128] = 16;

            sizes[(byte)TypeEnum.Float16] = 2;
            sizes[(byte)TypeEnum.Float32] = (short)Unsafe.SizeOf<float>();
            sizes[(byte)TypeEnum.Float64] = (short)Unsafe.SizeOf<double>();
            sizes[(byte)TypeEnum.Float128] = 16;

            sizes[(byte)TypeEnum.Decimal32] = 4;
            sizes[(byte)TypeEnum.Decimal64] = 8;
            sizes[(byte)TypeEnum.Decimal128] = 16;

            sizes[(byte)TypeEnum.Decimal] = 16;
            sizes[(byte)TypeEnum.SmallDecimal] = SmallDecimal.Size;

            sizes[(byte)TypeEnum.Bool] = 1;
            sizes[(byte)TypeEnum.Utf16Char] = 2;
            sizes[(byte)TypeEnum.UUID] = 16;

            sizes[(byte)TypeEnum.DateTime] = 8;
            sizes[(byte)TypeEnum.Timestamp] = Timestamp.Size;

            sizes[(byte)TypeEnum.Symbol] = Symbol.Size;
            sizes[(byte)TypeEnum.Symbol32] = Symbol32.Size;
            sizes[(byte)TypeEnum.Symbol64] = Symbol64.Size;
            sizes[(byte)TypeEnum.Symbol128] = Symbol128.Size;
            sizes[(byte)TypeEnum.Symbol256] = Symbol256.Size;

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
        /// Returns a positive size of top-level <see cref="TypeEnum"/>
        /// for scalars or -1 for composite types, which could still be
        /// fixed size. Use <see cref="DataTypeHeader.Size"/> property
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
        public DataTypeHeader Header;

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

        public static readonly DataTypeHeader DataTypeHeader = TypeInfo.Header;

        public static readonly short FixedSize = TypeInfo.FixedSize;
        public static readonly bool IsFixedSize = FixedSize > 0;

        internal static readonly DataTypeHeader DefaultBinaryHeader = new DataTypeHeader
        {
            VersionAndFlags =
            {
                ConverterVersion = TypeHelper<T>.BinarySerializer?.SerializerVersion ?? 0,
                IsBinary = true,
                CompressionMethod = CompressionMethod.None
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
                || te == TypeEnum.FixedSize)
            {
                return new TypeInfo<T>
                {
                    // Scalars have only one field set
                    Header = new DataTypeHeader
                    {
                        TEOFS = teofs
                    },
                    FixedSize = teofs.Size > 0 ? teofs.Size : TypeHelper<T>.PinnedSize
                };
            }

            if (te == TypeEnum.Array)
            {
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeader>(
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

            if (te == TypeEnum.TupleTN)
            {
                // Here we need to leave count slot at zero and as a special case write it from BS

                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeader>(
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

            if (te == TypeEnum.TupleT2
                || te == TypeEnum.TupleT3
                || te == TypeEnum.TupleT4
                || te == TypeEnum.TupleT5
                || te == TypeEnum.TupleT6
            )
            {
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeader>(
                    nameof(CreateFixedTupleTHeader),
                    typeof(T).GetGenericArguments()[0]
                );
                var header = func();
                var tbs = TypeHelper<T>.BinarySerializer;
                var fs = tbs?.FixedSize ?? -1;
                return new TypeInfo<T>
                {
                    Header = header,
                    FixedSize = fs
                };
            }

            if (te == TypeEnum.Tuple2
                || te == TypeEnum.Tuple2Byte
                || te == TypeEnum.Tuple2Long
            )
            {
                var gArgs = typeof(T).GetGenericArguments();
                var func = ReflectionHelper.MakeFunc<TypeEnumHelper<T>, DataTypeHeader>(
                    nameof(CreateTuple2Header),
                    gArgs[0], gArgs[1]
                );
                var header = func();
                var tbs = TypeHelper<T>.BinarySerializer;
                var fs = tbs?.FixedSize ?? -1;
                return new TypeInfo<T>
                {
                    Header = header,
                    FixedSize = fs
                };
            }

            if (te == TypeEnum.UserType)
            {
                short fs = 0;
                if (TypeHelper<T>.BinarySerializer != null)
                {
                    fs = Math.Max((short)0, TypeHelper<T>.BinarySerializer.FixedSize);
                }

                return new TypeInfo<T>
                {
                    Header = new DataTypeHeader
                    {
                        TEOFS = new TypeEnumOrFixedSize(te),
                        UserFixedSize = fs
                    },
                    FixedSize = fs
                };
            }

            return new TypeInfo<T>
            {
                Header = new DataTypeHeader { TEOFS = new TypeEnumOrFixedSize(te) },
                FixedSize = -1
            };

            throw new NotImplementedException();
        }

        /// <summary>
        /// Recursively get elemnt type info, fuse jagged arrays.
        /// </summary>
        private static DataTypeHeader CreateArrayInfo<TElement>()
        {
            var elHeader = TypeEnumHelper<TElement>.DataTypeHeader;

            if (elHeader.TEOFS.TypeEnum == TypeEnum.JaggedArray)
            {
                // Fuse jagged arrays.
                return new DataTypeHeader
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnum.JaggedArray),
                    TEOFS1 = elHeader.TEOFS1,
                    TEOFS2 = new TypeEnumOrFixedSize((byte)(elHeader.TEOFS2.Size + 1)), // JA depth
                };
            }

            if (elHeader.TEOFS.TypeEnum == TypeEnum.Array)
            {
                if (elHeader.TEOFS2 != default)
                {
                    elHeader.TEOFS1 = new TypeEnumOrFixedSize(TypeEnum.CompositeType);
                    elHeader.TEOFS2 = default;
                }
                return new DataTypeHeader
                {
                    TEOFS = new TypeEnumOrFixedSize(TypeEnum.JaggedArray),
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
                return new DataTypeHeader
                {
                    TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                    TEOFS1 = new TypeEnumOrFixedSize(TypeEnum.CompositeType),
                };
            }
            return new DataTypeHeader
            {
                TEOFS = TypeEnumHelper<TElement[]>.GetTeofs(),
                TEOFS1 = elHeader.TEOFS,
                TEOFS2 = elHeader.TEOFS1
            };
        }

        private static DataTypeHeader CreateFixedArrayTHeader<T1>()
        {
            Debug.Assert(GetTeofs().TypeEnum == TypeEnum.TupleTN);
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;

            if (!t1Header.IsScalar)
            {
                return new DataTypeHeader
                {
                    TEOFS = GetTeofs(), // Parent
                    TupleNCount = default,
                    TupleTNTeofs = new TypeEnumOrFixedSize(TypeEnum.CompositeType)
                };
            }
            return new DataTypeHeader
            {
                TEOFS = GetTeofs(),
                TEOFS1 = default, // Must be overwritten by BS
                TEOFS2 = t1Header.TEOFS1
            };
        }

        private static DataTypeHeader CreateFixedTupleTHeader<T1>()
        {
            Debug.Assert(GetTeofs().TypeEnum == TypeEnum.TupleT2);
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;

            // Note: TEOFS2 not IsScalar, fixed tuples could contain types that require 2 slots, e.g. Tuple2<Tuple2,Tuple2> or Tuple2<int[],int[]> TODO test those
            if (t1Header.TEOFS2 != default)
            {
                return new DataTypeHeader()
                {
                    TEOFS = GetTeofs(), // Parent
                    TEOFS1 = new TypeEnumOrFixedSize(TypeEnum.CompositeType)
                };
            }
            return new DataTypeHeader
            {
                TEOFS = GetTeofs(),
                TEOFS1 = t1Header.TEOFS,
                TEOFS2 = t1Header.TEOFS1
            };
        }

        private static DataTypeHeader CreateTuple2Header<T1, T2>()
        {
            var t1Header = TypeEnumHelper<T1>.DataTypeHeader;
            if (!t1Header.IsScalar)
            {
                t1Header.TEOFS = new TypeEnumOrFixedSize(TypeEnum.CompositeType);
            }

            var t2Header = TypeEnumHelper<T2>.DataTypeHeader;
            if (!t2Header.IsScalar)
            {
                t2Header.TEOFS = new TypeEnumOrFixedSize(TypeEnum.CompositeType);
                t2Header.TEOFS1 = default;
            }

            return new DataTypeHeader
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
            if (topTe == TypeEnum.FixedSize)
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
        private static TypeEnum GetTypeEnum()
        {
            if (typeof(T) == typeof(bool)) return TypeEnum.Bool;
            if (typeof(T) == typeof(byte)) return TypeEnum.UInt8;
            if (typeof(T) == typeof(char)) return TypeEnum.Utf16Char;
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
            if (typeof(T) == typeof(SmallDecimal)) return TypeEnum.SmallDecimal;

            if (typeof(T) == typeof(UUID)) return TypeEnum.UUID;

            if (typeof(T) == typeof(DateTime)) return TypeEnum.DateTime;
            if (typeof(T) == typeof(Timestamp)) return TypeEnum.Timestamp;

            if (typeof(T) == typeof(Symbol)) return TypeEnum.Symbol;
            if (typeof(T) == typeof(Symbol32)) return TypeEnum.Symbol32;
            if (typeof(T) == typeof(Symbol64)) return TypeEnum.Symbol64;
            if (typeof(T) == typeof(Symbol128)) return TypeEnum.Symbol128;
            if (typeof(T) == typeof(Symbol256)) return TypeEnum.Symbol256;

            if (typeof(T) == typeof(byte[])) return TypeEnum.Binary;
            if (typeof(T) == typeof(Memory<byte>)) return TypeEnum.Binary;
            if (typeof(T) == typeof(DirectBuffer)) return TypeEnum.Binary;
            if (typeof(T) == typeof(RetainedMemory<byte>)) return TypeEnum.Binary;

            if (typeof(T) == typeof(string)) return TypeEnum.Utf16String;

            if (typeof(T) == typeof(Json)) return TypeEnum.Json;

            #region Tuple-like

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(FixedArray<>))
            {
                return TypeEnum.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnum.TupleT2;
                }
                return TypeEnum.Tuple2;
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
                    return TypeEnum.TupleT2;
                }
                return TypeEnum.Tuple2;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnum.TupleT3;
                }
                return TypeEnum.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnum.TupleT4;
                }
                return TypeEnum.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnum.TupleT5;
                }
                return TypeEnum.TupleTN;
            }

            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>))
            {
                if (ReflectionHelper.SameGenericArgs<T>())
                {
                    return TypeEnum.TupleT6;
                }
                return TypeEnum.TupleTN;
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
                    return TypeEnum.TupleTN;
                }
                return TypeEnum.TupleTN;
            }

            // TODO Tuple2Version, KeyIndexValue
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(TaggedKeyValue<,>)) return TypeEnum.Tuple2Byte;

            #endregion Tuple-like

            // TODO(?) Memory manager, RetainableMemory, however they have Memory field and we are serializing *Memory*, not a container, in that case.
            if (typeof(T).IsArray) return TypeEnum.Array;
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(Memory<>)) { return TypeEnum.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(RetainedMemory<>)) { return TypeEnum.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(VectorStorage<>)) { return TypeEnum.Array; }
            if (typeof(T).GetTypeInfo().IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ArrayWrapper<>)) { return TypeEnum.Array; }

            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.Matrix<>)) return TypeEnum.NDArray;
            if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Spreads.Collections.Internal.Experimental.NDArray<>)) return TypeEnum.NDArray;

            // if (typeof(T).GetTypeInfo().IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ValueWithTimestamp<>)) return TypeEnumEx.ValueWithTimestamp;

            // TODO next 2 commented temporarily
            //if (typeof(T) == typeof(Table)) return TypeEnumEx.Table;

            //if (typeof(T) == typeof(Variant)) return TypeEnumEx.Variant;

            // Order of the three following checks is important.
            // Fixed types should rarely have a custom serializer
            // but if they then the serializer is more important.

            if (TypeHelper<T>.HasBinarySerializer && !TypeHelper<T>.IsInternalBinarySerializer)
            {
                return TypeEnum.UserType;
            }

            if (TypeHelper<T>.FixedSize > 0)
            {
                return TypeEnum.FixedSize;
            }

            return TypeEnum.UserType;
        }
    }
}
