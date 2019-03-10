// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.DataTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization.Serializers
{
    internal class TuplePackSerializer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteItem<T>(BufferWriter bufferWriter, in T value)
        {
            var offset = bufferWriter.Offset;
            var s1 = FixedProxy<T>.SizeOf(in value, bufferWriter);
            if (bufferWriter.Offset == offset)
            {
                bufferWriter.EnsureCapacity(s1);
                var written = FixedProxy<T>.Write(in value, bufferWriter.FreeBuffer);
                if (written != s1)
                {
                    BinarySerializer.FailWrongSerializerImplementation<T>(BinarySerializer.BinarySerializerFailCode
                        .WrittenNotEqualToSizeOf);
                }

                bufferWriter.Advance(written);
            }

            return s1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadItem<T>(DirectBuffer source, out T item)
        {
            var consumed = FixedProxy<T>.Read(source, out item);
            if (consumed <= 0)
            {
                FailShouldNotBeCalled();
            }

            return consumed;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void FailShouldNotBeCalled()
        {
            ThrowHelper.FailFast(
                "Binary serialization is only supported for fixed-size tuples." +
                " A custom serializer must not be registered for other tuples.");
        }
    }

    #region Tuple2

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

    internal static class TuplePackSerializer<T1, T2>
    {
        // ReSharper disable once StaticMemberInGenericType
        internal static readonly short FixedSize = CalculateFixedSize();

        internal static readonly bool HasAnySerializer = TypeHelper<T1>.HasTypeSerializer
                                                          || TypeHelper<T2>.HasTypeSerializer
                                                          ;

        private static short CalculateFixedSize()
        {
            var s1 = TypeEnumHelper<T1>.FixedSize;
            var s2 = TypeEnumHelper<T2>.FixedSize;
            if (s1 > 0 && s2 > 0)
            {
                return checked((short)(s1 + s2));
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(in TuplePack<T1, T2> value, BufferWriter bufferWriter)
        {
            if (HasAnySerializer)
            {
                Debug.Assert(bufferWriter != null);

                var s1 = TuplePackSerializer.WriteItem<T1>(bufferWriter, in value.Item1);
                var s2 = TuplePackSerializer.WriteItem<T2>(bufferWriter, in value.Item2);

                return s1 + s2;
            }

            if (FixedSize > 0)
            {
                // this will save a Write call later
                bufferWriter?.Write(in value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(in TuplePack<T1, T2> value, DirectBuffer destination)
        {
            if (HasAnySerializer)
            {
                var bufferWriter = BufferWriter.Create();
                var sizeOf = SizeOf(value, bufferWriter);
                bufferWriter.WrittenBuffer.CopyTo(destination);
                bufferWriter.Dispose();
                return sizeOf;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(TypeHelper<TuplePack<T1, T2>>.PinnedSize > 0);
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2>>());
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2>>.PinnedSize);

                // this does bounds check unless it is turned off
                destination.Write(0, value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(DirectBuffer source, out TuplePack<T1, T2> value)
        {
            if (HasAnySerializer)
            {
                var acc = 0;
                int consumed;

                acc += consumed = TuplePackSerializer.ReadItem<T1>(source, out var item1);

                source = source.Slice(consumed);
                acc += TuplePackSerializer.ReadItem<T2>(source, out var item2);

                value = new TuplePack<T1, T2>((item1, item2));
                return acc;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(TypeHelper<TuplePack<T1, T2>>.PinnedSize > 0);
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2>>());
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2>>.PinnedSize);

                value = source.Read<TuplePack<T1, T2>>(0);
                return FixedSize;
            }

            value = default;
            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }
    }

    internal static class ValueTuple2SerializerFactory
    {
        public static BinarySerializer<(T1, T2)> GenericCreate<T1, T2>()
        {
            return new ValueTuple2Serializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(ValueTuple2SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class ValueTuple2Serializer<T1, T2> : InternalSerializer<(T1, T2)>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => TuplePackSerializer<T1, T2>.FixedSize;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in (T1, T2) value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2>.SizeOf(new TuplePack<T1, T2>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in (T1, T2) value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2>.Write(new TuplePack<T1, T2>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out (T1, T2) value)
            {
                var readBytes = TuplePackSerializer<T1, T2>.Read(source, out var tp);
                value = (tp.Item1, tp.Item2);
                return readBytes;
            }
        }
    }

    internal static class Tuple2SerializerFactory
    {
        public static BinarySerializer<Tuple<T1, T2>> GenericCreate<T1, T2>()
        {
            return new Tuple2Serializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(Tuple2SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }
    }

    internal sealed class Tuple2Serializer<T1, T2> : InternalSerializer<Tuple<T1, T2>>
    {
        public override byte KnownTypeId => 0;

        public override short FixedSize => TuplePackSerializer<T1, T2>.FixedSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int SizeOf(in Tuple<T1, T2> value, BufferWriter payload)
        {
            return TuplePackSerializer<T1, T2>.SizeOf(new TuplePack<T1, T2>(value), payload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Write(in Tuple<T1, T2> value, DirectBuffer destination)
        {
            return TuplePackSerializer<T1, T2>.Write(new TuplePack<T1, T2>(value), destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(DirectBuffer source, out Tuple<T1, T2> value)
        {
            var readBytes = TuplePackSerializer<T1, T2>.Read(source, out var tp);
            value = Tuple.Create(tp.Item1, tp.Item2);
            return readBytes;
        }
    }

    internal static class KvpSerializerFactory
    {
        public static BinarySerializer<KeyValuePair<T1, T2>> GenericCreate<T1, T2>()
        {
            return new KvpSerializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(KvpSerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class KvpSerializer<T1, T2> : InternalSerializer<KeyValuePair<T1, T2>>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize => TuplePackSerializer<T1, T2>.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in KeyValuePair<T1, T2> value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2>.SizeOf(new TuplePack<T1, T2>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in KeyValuePair<T1, T2> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2>.Write(new TuplePack<T1, T2>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out KeyValuePair<T1, T2> value)
            {
                var readBytes = TuplePackSerializer<T1, T2>.Read(source, out var tp);
                value = new KeyValuePair<T1, T2>(tp.Item1, tp.Item2);
                return readBytes;
            }
        }
    }

    #endregion Tuple2

    #region Tuple3

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

    internal static class TuplePackSerializer<T1, T2, T3>
    {
        // ReSharper disable once StaticMemberInGenericType
        internal static readonly short FixedSize = CalculateFixedSize();

        internal static readonly bool HasAnySerializer = TypeHelper<T1>.HasTypeSerializer
                                                          || TypeHelper<T2>.HasTypeSerializer
                                                          || TypeHelper<T3>.HasTypeSerializer
                                                          ;

        private static short CalculateFixedSize()
        {
            var s1 = TypeEnumHelper<T1>.FixedSize;
            var s2 = TypeEnumHelper<T2>.FixedSize;
            var s3 = TypeEnumHelper<T3>.FixedSize;
            if (s1 > 0
                && s2 > 0
                && s3 > 0
                )
            {
                return checked((short)(s1
                                       + s2
                                       + s3
                                       ));
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(in TuplePack<T1, T2, T3> value, BufferWriter bufferWriter)
        {
            if (HasAnySerializer)
            {
                Debug.Assert(bufferWriter != null);

                var s1 = TuplePackSerializer.WriteItem<T1>(bufferWriter, in value.Item1);
                var s2 = TuplePackSerializer.WriteItem<T2>(bufferWriter, in value.Item2);
                var s3 = TuplePackSerializer.WriteItem<T3>(bufferWriter, in value.Item3);

                return s1 + s2 + s3;
            }

            if (FixedSize > 0)
            {
                // this will save a Write call later
                bufferWriter?.Write(in value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(in TuplePack<T1, T2, T3> value, DirectBuffer destination)
        {
            if (HasAnySerializer)
            {
                var bufferWriter = BufferWriter.Create();
                var sizeOf = SizeOf(value, bufferWriter);
                bufferWriter.WrittenBuffer.CopyTo(destination);
                bufferWriter.Dispose();
                return sizeOf;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize > 0);
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3>>());
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize);

                // this does bounds check unless it is turned off
                destination.Write(0, value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(DirectBuffer source, out TuplePack<T1, T2, T3> value)
        {
            if (HasAnySerializer)
            {
                var acc = 0;
                int consumed;

                acc += consumed = TuplePackSerializer.ReadItem<T1>(source, out var item1);

                source = source.Slice(consumed);
                acc += consumed = TuplePackSerializer.ReadItem<T2>(source, out var item2);

                source = source.Slice(consumed);
                acc += TuplePackSerializer.ReadItem<T3>(source, out var item3);

                value = new TuplePack<T1, T2, T3>((item1, item2, item3));
                return acc;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize > 0);
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3>>());
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3>>.PinnedSize);

                value = source.Read<TuplePack<T1, T2, T3>>(0);
                return FixedSize;
            }

            value = default;
            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }
    }

    internal static class ValueTuple3SerializerFactory
    {
        public static BinarySerializer<(T1, T2, T3)> GenericCreate<T1, T2, T3>()
        {
            return new ValueTuple3Serializer<T1, T2, T3>();
        }

        public static object Create(Type type1, Type type2, Type type3)
        {
            var method = typeof(ValueTuple3SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3);
            return generic?.Invoke(null, null);
        }

        internal sealed class ValueTuple3Serializer<T1, T2, T3> : InternalSerializer<(T1, T2, T3)>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize => TuplePackSerializer<T1, T2, T3>.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in (T1, T2, T3) value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2, T3>.SizeOf(new TuplePack<T1, T2, T3>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in (T1, T2, T3) value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3>.Write(new TuplePack<T1, T2, T3>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out (T1, T2, T3) value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3>.Read(source, out var tp);
                value = (tp.Item1, tp.Item2, tp.Item3);
                return readBytes;
            }
        }
    }

    internal static class Tuple3SerializerFactory
    {
        public static BinarySerializer<Tuple<T1, T2, T3>> GenericCreate<T1, T2, T3>()
        {
            return new Tuple3Serializer<T1, T2, T3>();
        }

        public static object Create(Type type1, Type type2, Type type3)
        {
            var method = typeof(Tuple3SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3);
            return generic?.Invoke(null, null);
        }

        internal sealed class Tuple3Serializer<T1, T2, T3> : InternalSerializer<Tuple<T1, T2, T3>>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize => TuplePackSerializer<T1, T2, T3>.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in Tuple<T1, T2, T3> value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2, T3>.SizeOf(new TuplePack<T1, T2, T3>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in Tuple<T1, T2, T3> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3>.Write(new TuplePack<T1, T2, T3>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out Tuple<T1, T2, T3> value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3>.Read(source, out var tp);
                value = Tuple.Create(tp.Item1, tp.Item2, tp.Item3);
                return readBytes;
            }
        }
    }

    internal static class TaggedKeyValueByteSerializerFactory
    {
        public static BinarySerializer<TaggedKeyValue<T1, T2>> GenericCreate<T1, T2>()
        {
            return new TaggedKeyValueSerializer<T1, T2>();
        }

        public static object Create(Type type1, Type type2)
        {
            var method = typeof(TaggedKeyValueByteSerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2);
            return generic?.Invoke(null, null);
        }

        internal sealed class TaggedKeyValueSerializer<T1, T2> : InternalSerializer<TaggedKeyValue<T1, T2>>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => TuplePackSerializer<byte, T1, T2>.FixedSize;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in TaggedKeyValue<T1, T2> value, BufferWriter payload)
            {
                return TuplePackSerializer<byte, T1, T2>.SizeOf(Unsafe.As<TaggedKeyValue<T1, T2>, TuplePack<byte, T1, T2>>(ref Unsafe.AsRef(in value)), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in TaggedKeyValue<T1, T2> value, DirectBuffer destination)
            {
                return TuplePackSerializer<byte, T1, T2>.Write(Unsafe.As<TaggedKeyValue<T1, T2>, TuplePack<byte, T1, T2>>(ref Unsafe.AsRef(in value)), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out TaggedKeyValue<T1, T2> value)
            {
                var readBytes = TuplePackSerializer<byte, T1, T2>.Read(source, out var tp);
                value = Unsafe.As<TuplePack<byte, T1, T2>, TaggedKeyValue<T1, T2>>(ref tp);

                return readBytes;
            }
        }
    }

    #endregion Tuple3

    #region Tuple4

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BinarySerialization(preferBlittable: true)]
    internal struct TuplePack<T1, T2, T3, T4>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
        public T4 Item4;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack((T1, T2, T3, T4) tuple)
        {
            (Item1, Item2, Item3, Item4) = tuple;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TuplePack(Tuple<T1, T2, T3, T4> tuple)
        {
            (Item1, Item2, Item3, Item4) = tuple;
        }
    }

    internal static class TuplePackSerializer<T1, T2, T3, T4>
    {
        // ReSharper disable once StaticMemberInGenericType
        internal static readonly short FixedSize = CalculateFixedSize();

        internal static readonly bool HasAnySerializer = TypeHelper<T1>.HasTypeSerializer
                                                          || TypeHelper<T2>.HasTypeSerializer
                                                          || TypeHelper<T3>.HasTypeSerializer
                                                          || TypeHelper<T4>.HasTypeSerializer
                                                          ;

        private static short CalculateFixedSize()
        {
            var s1 = TypeEnumHelper<T1>.FixedSize;
            var s2 = TypeEnumHelper<T2>.FixedSize;
            var s3 = TypeEnumHelper<T3>.FixedSize;
            var s4 = TypeEnumHelper<T4>.FixedSize;
            if (s1 > 0
                && s2 > 0
                && s3 > 0
                && s4 > 0
                )
            {
                return checked((short)(s1
                                       + s2
                                       + s3
                                       + s4
                                       ));
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(in TuplePack<T1, T2, T3, T4> value, BufferWriter bufferWriter)
        {
            if (HasAnySerializer)
            {
                Debug.Assert(bufferWriter != null);

                var s1 = TuplePackSerializer.WriteItem<T1>(bufferWriter, in value.Item1);
                var s2 = TuplePackSerializer.WriteItem<T2>(bufferWriter, in value.Item2);
                var s3 = TuplePackSerializer.WriteItem<T3>(bufferWriter, in value.Item3);
                var s4 = TuplePackSerializer.WriteItem<T4>(bufferWriter, in value.Item4);

                return s1 + s2 + s3 + s4;
            }

            if (FixedSize > 0)
            {
                // this will save a Write call later
                bufferWriter?.Write(in value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(in TuplePack<T1, T2, T3, T4> value, DirectBuffer destination)
        {
            if (HasAnySerializer)
            {
                var bufferWriter = BufferWriter.Create();
                var sizeOf = SizeOf(value, bufferWriter);
                bufferWriter.WrittenBuffer.CopyTo(destination);
                bufferWriter.Dispose();
                return sizeOf;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(TypeHelper<TuplePack<T1, T2, T3, T4>>.PinnedSize > 0);
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3, T4>>());
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3, T4>>.PinnedSize);

                // this does bounds check unless it is turned off
                destination.Write(0, value);
                return FixedSize;
            }

            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(DirectBuffer source, out TuplePack<T1, T2, T3, T4> value)
        {
            if (HasAnySerializer)
            {
                var acc = 0;
                int consumed;

                acc += consumed = TuplePackSerializer.ReadItem<T1>(source, out var item1);

                source = source.Slice(consumed);
                acc += consumed = TuplePackSerializer.ReadItem<T2>(source, out var item2);

                source = source.Slice(consumed);
                acc += consumed = TuplePackSerializer.ReadItem<T3>(source, out var item3);

                source = source.Slice(consumed);
                acc += TuplePackSerializer.ReadItem<T4>(source, out var item4);

                value = new TuplePack<T1, T2, T3, T4>((item1, item2, item3, item4));
                return acc;
            }

            if (FixedSize > 0)
            {
                Debug.Assert(FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3, T4>>(), "FixedSize == Unsafe.SizeOf<TuplePack<T1, T2, T3, T4>>()");
                Debug.Assert(FixedSize == TypeHelper<TuplePack<T1, T2, T3, T4>>.FixedSize, "FixedSize == TypeHelper<TuplePack<T1, T2, T3, T4>>.FixedSize");

                value = source.Read<TuplePack<T1, T2, T3, T4>>(0);
                return FixedSize;
            }

            value = default;
            TuplePackSerializer.FailShouldNotBeCalled();
            return -1;
        }
    }

    internal static class ValueTuple4SerializerFactory
    {
        public static BinarySerializer<(T1, T2, T3, T4)> GenericCreate<T1, T2, T3, T4>()
        {
            return new ValueTuple4Serializer<T1, T2, T3, T4>();
        }

        public static object Create(Type type1, Type type2, Type type3, Type type4)
        {
            var method = typeof(ValueTuple4SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3, type4);
            return generic?.Invoke(null, null);
        }

        internal sealed class ValueTuple4Serializer<T1, T2, T3, T4> : InternalSerializer<(T1, T2, T3, T4)>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize => TuplePackSerializer<T1, T2, T3, T4>.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in (T1, T2, T3, T4) value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2, T3, T4>.SizeOf(new TuplePack<T1, T2, T3, T4>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in (T1, T2, T3, T4) value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3, T4>.Write(new TuplePack<T1, T2, T3, T4>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out (T1, T2, T3, T4) value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3, T4>.Read(source, out var tp);
                value = (tp.Item1, tp.Item2, tp.Item3, tp.Item4);
                return readBytes;
            }
        }
    }

    internal static class Tuple4SerializerFactory
    {
        public static BinarySerializer<Tuple<T1, T2, T3, T4>> GenericCreate<T1, T2, T3, T4>()
        {
            return new Tuple4Serializer<T1, T2, T3, T4>();
        }

        public static object Create(Type type1, Type type2, Type type3, Type type4)
        {
            var method = typeof(Tuple4SerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type1, type2, type3, type4);
            return generic?.Invoke(null, null);
        }

        internal sealed class Tuple4Serializer<T1, T2, T3, T4> : InternalSerializer<Tuple<T1, T2, T3, T4>>
        {
            public override byte KnownTypeId => 0;

            public override short FixedSize => TuplePackSerializer<T1, T2, T3, T4>.FixedSize;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int SizeOf(in Tuple<T1, T2, T3, T4> value, BufferWriter payload)
            {
                return TuplePackSerializer<T1, T2, T3, T4>.SizeOf(new TuplePack<T1, T2, T3, T4>(value), payload);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Write(in Tuple<T1, T2, T3, T4> value, DirectBuffer destination)
            {
                return TuplePackSerializer<T1, T2, T3, T4>.Write(new TuplePack<T1, T2, T3, T4>(value), destination);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override int Read(DirectBuffer source, out Tuple<T1, T2, T3, T4> value)
            {
                var readBytes = TuplePackSerializer<T1, T2, T3, T4>.Read(source, out var tp);
                value = Tuple.Create(tp.Item1, tp.Item2, tp.Item3, tp.Item4);
                return readBytes;
            }
        }
    }

    #endregion Tuple4
}
