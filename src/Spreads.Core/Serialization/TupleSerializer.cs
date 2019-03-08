// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.DataTypes;

namespace Spreads.Serialization
{
    #region Tuple2

    /// <summary>
    /// Tight packing of a <see cref="ValueTuple{T1,T2}"/>.
    /// Is blittable if every element if blittable.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BinarySerialization(preferBlittable: true)]
    internal struct TuplePack<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack((T1, T2) tuple)
        {
            (Item1, Item2) = tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack(KeyValuePair<T1, T2> kvp)
        {
            (Item1, Item2) = (kvp.Key, kvp.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack(Tuple<T1, T2> tuple)
        {
            (Item1, Item2) = tuple;
        }
    }

    internal sealed class TuplePackSerializer<T1, T2> : TuplePackSerializer, IBinarySerializer<TuplePack<T1, T2>>
    {
        public static readonly TuplePackSerializer<T1, T2> Instance = new TuplePackSerializer<T1, T2>();

        private TuplePackSerializer()
        { }

        public byte SerializerVersion => 0;

        public byte KnownTypeId => 0;

        // ReSharper disable once StaticMemberInGenericType
        private static readonly short FixedSizeInternal = CalculateFixedSize();

        private static short CalculateFixedSize()
        {
            if (typeof(T1) == typeof(T2))
            {
                // -2 is a valid indicator of var size, as well as any negative number
                return checked((short)(TypeEnumHelper<T1>.FixedSize * 2));
            }

            var s1 = TypeEnumHelper<T1>.FixedSize;
            var s2 = TypeEnumHelper<T2>.FixedSize;
            if (s1 > 0 && s2 > 0)
            {
                return checked((short)(s1 + s2));
            }

            return -1;
        }

        public short FixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FixedSizeInternal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(in TuplePack<T1, T2> value, out RetainedMemory<byte> temporaryBuffer)
        {
            if (FixedSize <= 0)
            {
                FailNotFixedSize();
            }

            temporaryBuffer = default;
            return FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(in TuplePack<T1, T2> value, DirectBuffer destination)
        {
            Debug.Assert(TypeHelper<TuplePack<T1, T2>>.PinnedSize > 0);
            Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2>>());
            Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2>>.PinnedSize);

            // this does bounds check unless it is turned off
            destination.Write(0, value);
            return FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(DirectBuffer source, out TuplePack<T1, T2> value)
        {
            Debug.Assert(TypeHelper<TuplePack<T1, T2>>.PinnedSize > 0);
            Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2>>());
            Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2>>.PinnedSize);

            value = source.Read<TuplePack<T1, T2>>(0);
            return FixedSize;
        }
    }

    internal static class ValueTuple2SerializerFactory
    {
        public static IBinarySerializer<(T1, T2)> GenericCreate<T1, T2>()
        {
            return new ValueTuple2Serializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(ValueTuple2SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class ValueTuple2Serializer<T1, T2> : IBinarySerializer<(T1, T2)>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize => TuplePackSerializer<T1, T2>.Instance.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in (T1, T2) value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<T1, T2>.Instance.SizeOf(new TuplePack<T1, T2>(value), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in (T1, T2) value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2>.Instance.Write(new TuplePack<T1, T2>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out (T1, T2) value)
            {
                var readBytes = TuplePackSerializer<T1, T2>.Instance.Read(source, out var tp);
                value = (tp.Item1, tp.Item2);
                return readBytes;
            }
        }
    }

    internal static class Tuple2SerializerFactory
    {
        public static IBinarySerializer<Tuple<T1, T2>> GenericCreate<T1, T2>()
        {
            return new Tuple2Serializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(Tuple2SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class Tuple2Serializer<T1, T2> : IBinarySerializer<Tuple<T1, T2>>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize => TuplePackSerializer<T1, T2>.Instance.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in Tuple<T1, T2> value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<T1, T2>.Instance.SizeOf(new TuplePack<T1, T2>(value), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in Tuple<T1, T2> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2>.Instance.Write(new TuplePack<T1, T2>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out Tuple<T1, T2> value)
            {
                var readBytes = TuplePackSerializer<T1, T2>.Instance.Read(source, out var tp);
                value = Tuple.Create(tp.Item1, tp.Item2);
                return readBytes;
            }
        }
    }

    internal static class KvpSerializerFactory
    {
        public static IBinarySerializer<KeyValuePair<T1, T2>> GenericCreate<T1, T2>()
        {
            return new KvpSerializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(KvpSerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class KvpSerializer<T1, T2> : IBinarySerializer<KeyValuePair<T1, T2>>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize => TuplePackSerializer<T1, T2>.Instance.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in KeyValuePair<T1, T2> value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<T1, T2>.Instance.SizeOf(new TuplePack<T1, T2>(value), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in KeyValuePair<T1, T2> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2>.Instance.Write(new TuplePack<T1, T2>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out KeyValuePair<T1, T2> value)
            {
                var readBytes = TuplePackSerializer<T1, T2>.Instance.Read(source, out var tp);
                value = new KeyValuePair<T1, T2>(tp.Item1, tp.Item2);
                return readBytes;
            }
        }
    }

    #endregion Tuple2

    #region Tuple3

    /// <summary>
    /// Tight packing of a <see cref="ValueTuple{T1,T2}"/>.
    /// Is blittable if every element if blittable.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BinarySerialization(preferBlittable: true)]
    internal struct TuplePack<T1, T2, T3>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack((T1, T2, T3) tuple)
        {
            (Item1, Item2, Item3) = tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack(Tuple<T1, T2, T3> tuple)
        {
            (Item1, Item2, Item3) = tuple;
        }
    }

    internal sealed class TuplePackSerializer<T1, T2, T3> : TuplePackSerializer, IBinarySerializer<TuplePack<T1, T2, T3>>
    {
        public static readonly TuplePackSerializer<T1, T2, T3> Instance = new TuplePackSerializer<T1, T2, T3>();

        private TuplePackSerializer()
        { }

        public byte SerializerVersion => 0;

        public byte KnownTypeId => 0;

        // ReSharper disable once StaticMemberInGenericType
        internal static readonly short FixedSizeInternal = CalculateFixedSize();

        private static short CalculateFixedSize()
        {
            if (typeof(T1) == typeof(T2)
                && typeof(T1) == typeof(T3)
            )
            {
                // -2 is a valid indicator of var size, as well as any negative number
                return checked((short)(TypeEnumHelper<T1>.FixedSize * 3));
            }

            var s1 = TypeEnumHelper<T1>.FixedSize;
            var s2 = TypeEnumHelper<T2>.FixedSize;
            var s3 = TypeEnumHelper<T3>.FixedSize;
            if (s1 > 0 && s2 > 0 && s3 > 0)
            {
                return checked((short)(s1 + s2 + s3));
            }

            return -1;
        }

        public short FixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FixedSizeInternal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int SizeOf(in TuplePack<T1, T2, T3> value, out RetainedMemory<byte> temporaryBuffer)
        {
            if (FixedSize <= 0)
            {
                FailNotFixedSize();
            }

            temporaryBuffer = default;
            return FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(in TuplePack<T1, T2, T3> value, DirectBuffer destination)
        {
            Debug.Assert(TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize > 0);
            Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3>>());
            Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize);

            // this does bounds check unless it is turned off
            destination.Write(0, value);
            return FixedSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(DirectBuffer source, out TuplePack<T1, T2, T3> value)
        {
            Debug.Assert(TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize > 0);
            Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3>>());
            Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize);

            value = source.Read<TuplePack<T1, T2, T3>>(0);
            return FixedSize;
        }
    }

    internal static class ValueTuple3SerializerFactory
    {
        public static IBinarySerializer<(T1, T2, T3)> GenericCreate<T1, T2, T3>()
        {
            return new ValueTuple3Serializer<T1, T2, T3>();
        }

        public static object Create(Type type1, Type type2, Type type3)
        {
            var method = typeof(ValueTuple3SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3);
            return generic?.Invoke(null, null);
        }

        internal sealed class ValueTuple3Serializer<T1, T2, T3> : IBinarySerializer<(T1, T2, T3)>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize => TuplePackSerializer<T1, T2, T3>.Instance.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in (T1, T2, T3) value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<T1, T2, T3>.Instance.SizeOf(new TuplePack<T1, T2, T3>(value), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in (T1, T2, T3) value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3>.Instance.Write(new TuplePack<T1, T2, T3>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out (T1, T2, T3) value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3>.Instance.Read(source, out var tp);
                value = (tp.Item1, tp.Item2, tp.Item3);
                return readBytes;
            }
        }
    }

    internal static class Tuple3SerializerFactory
    {
        public static IBinarySerializer<Tuple<T1, T2, T3>> GenericCreate<T1, T2, T3>()
        {
            return new Tuple3Serializer<T1, T2, T3>();
        }

        public static object Create(Type type1, Type type2, Type type3)
        {
            var method = typeof(Tuple3SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3);
            return generic?.Invoke(null, null);
        }

        internal sealed class Tuple3Serializer<T1, T2, T3> : IBinarySerializer<Tuple<T1, T2, T3>>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize => TuplePackSerializer<T1, T2, T3>.Instance.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in Tuple<T1, T2, T3> value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<T1, T2, T3>.Instance.SizeOf(new TuplePack<T1, T2, T3>(value), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in Tuple<T1, T2, T3> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3>.Instance.Write(new TuplePack<T1, T2, T3>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out Tuple<T1, T2, T3> value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3>.Instance.Read(source, out var tp);
                value = Tuple.Create(tp.Item1, tp.Item2, tp.Item3);
                return readBytes;
            }
        }
    }

    internal static class TaggedKeyValueByteSerializerFactory
    {
        public static IBinarySerializer<TaggedKeyValue<T1, T2>> GenericCreate<T1, T2>()
        {
            return new TaggedKeyValueSerializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(TaggedKeyValueByteSerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class TaggedKeyValueSerializer<T1, T2> : IBinarySerializer<TaggedKeyValue<T1, T2>>
        {
            public byte SerializerVersion => 0;

            public byte KnownTypeId => 0;

            public short FixedSize
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => TuplePackSerializer<byte, T1, T2>.FixedSizeInternal;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int SizeOf(in TaggedKeyValue<T1, T2> value, out RetainedMemory<byte> temporaryBuffer)
            {
                return TuplePackSerializer<byte, T1, T2>.Instance.SizeOf(Unsafe.As<TaggedKeyValue<T1, T2>, TuplePack<byte, T1, T2>>(ref Unsafe.AsRef(in value)), out temporaryBuffer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Write(in TaggedKeyValue<T1, T2> value, DirectBuffer destination)
            {
                return TuplePackSerializer<byte, T1, T2>.Instance.Write(Unsafe.As<TaggedKeyValue<T1, T2>, TuplePack<byte, T1, T2>>(ref Unsafe.AsRef(in value)), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Read(DirectBuffer source, out TaggedKeyValue<T1, T2> value)
            {
                var readBytes = TuplePackSerializer<byte, T1, T2>.Instance.Read(source, out var tp);
                value = Unsafe.As< TuplePack<byte, T1, T2>, TaggedKeyValue<T1, T2>>(ref tp);
                return readBytes;
            }
        }
    }

    #endregion Tuple3

    internal class TuplePackSerializer
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void FailNotFixedSize()
        {
            ThrowHelper.FailFast(
                "Binary serialization is only supported for fixed-size tuples. A custom serializer must not be registered for var-size tuples.");
        }
    }
}
