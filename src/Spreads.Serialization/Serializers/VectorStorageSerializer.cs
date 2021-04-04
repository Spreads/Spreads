using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Collections.Internal
{
    internal static class VectorStorageSerializerFactory
    {
        public static BinarySerializer<RetainedVec<TElement>> GenericCreate<TElement>()
        {
            return new VectorStorageSerializer<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(VectorStorageSerializerFactory).GetTypeInfo().GetMethod("GenericCreate");
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }
    }

    // TODO fallback to ArrayWrapperSerializer with params
    // TODO do not throw not supported, just do not register a binary serializer
    // and add Json formatter.
    internal class VectorStorageSerializer<T> : InternalSerializer<RetainedVec<T>>
    {
        public override byte KnownTypeId => 0;

        public override short FixedSize => 0;

        public override int SizeOf(in RetainedVec<T> value, BufferWriter payload)
        {
            if (!TypeHelper<T>.IsFixedSize)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return 4 + value.Storage.Length * Unsafe.SizeOf<T>();
        }

        public override unsafe int Write(in RetainedVec<T> value, DirectBuffer destination)
        {
            if (!TypeHelper<T>.IsFixedSize)
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            // TODO we will use negative length as shuffled flag
            // but will need to add support for ArrayConverter for this.

            destination.Write(0, -value.Storage.Length); // Minus for shuffled
            if (value.Storage.Length > 0)
            {
                var byteLen = Unsafe.SizeOf<T>() * value.Storage.Length;
                Debug.Assert(destination.Length >= 4 + byteLen);

                if (TypeHelper<T>.IsIDelta)
                {
                    // one boxing TODO this is wrong direction, we need i - first, maybe add reverse delta or for binary it's OK?
                    var first = (IDelta<T>)value.Storage.UnsafeReadUnaligned<T>(0);
                    var arr = BufferPool<T>.Rent(value.Storage.Length);
                    arr[0] = value.Storage.UnsafeReadUnaligned<T>(0);
                    for (int i = 1; i < value.Storage.Length; i++)
                    {
                        arr[i] = first.GetDelta(value.Storage.UnsafeReadUnaligned<T>(i));
                    }

                    fixed (byte* sPtr = &Unsafe.As<T, byte>(ref arr[0]))
                    {
                        var source = new Span<byte>(sPtr, byteLen);
                        var destSpan = destination.Slice(4).Span;

                        BinarySerializer.Shuffle(source, destSpan, (byte)Unsafe.SizeOf<T>());
                    }

                    BufferPool<T>.Return(arr);
                }
                else
                {
                    fixed (byte* sPtr = &Unsafe.As<T, byte>(ref value.Storage.UnsafeGetRef<T>()))
                    {
                        var source = new Span<byte>(sPtr, byteLen);
                        var destSpan = destination.Slice(4).Span;

                        BinarySerializer.Shuffle(source, destSpan, (byte)Unsafe.SizeOf<T>());
                    }
                }

                return 4 + byteLen;
            }
            return 4;
        }

        public override unsafe int Read(DirectBuffer source, out RetainedVec<T> value)
        {
            var arraySize = source.Read<int>(0);
            if (arraySize > 0)
            {
                ThrowHelper.ThrowNotImplementedException("Non-shuffled arrays are not implemented yet"); // TODO
            }

            arraySize = -arraySize;

            // var position = 4;
            var payloadSize = arraySize * Unsafe.SizeOf<T>();
            if (4 + payloadSize > source.Length || arraySize < 0)
            {
                value = default;
                return -1;
            }

            if (arraySize > 0)
            {
                var byteLen = Unsafe.SizeOf<T>() * arraySize;
                var rm = BufferPool<T>.MemoryPool.RentMemory(arraySize);

                // TODO review, use unsafe methods (?)
                var vec = rm.GetVec();
                // ReSharper disable once PossibleNullReferenceException
                fixed (byte* dPtr = &Unsafe.As<T, byte>(ref vec.DangerousGetRef(0)))
                {
                    var srcDb = source.Slice(4).Span;
                    var destDb = new Span<byte>(dPtr, byteLen);

                    // srcDb.CopyTo(destDb);
                    BinarySerializer.Unshuffle(srcDb, destDb, (byte)Unsafe.SizeOf<T>());
                }

                if (TypeHelper<T>.IsIDelta)
                {
                    var first = (IDelta<T>)vec.DangerousGetUnaligned(0);
                    for (int i = 1; i < arraySize; i++)
                    {
                        vec.DangerousSetUnaligned(i, first.AddDelta(vec.DangerousGetUnaligned(i)));
                    }
                }

                var vs = RetainedVec.Create<T>(rm, 0, arraySize);

                value = new RetainedVec<T>(vs);
            }
            else
            {
                value = new RetainedVec<T>(default);
            }

            return 4 + payloadSize;
        }
    }
}
