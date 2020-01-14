using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Spreads.Buffers;
using Spreads.Native;
using Spreads.Serialization;

namespace Spreads.Collections.Internal
{
    internal static class VectorStorageSerializerFactory
    {
        public static BinarySerializer<VecStorage<TElement>> GenericCreate<TElement>()
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
    internal class VectorStorageSerializer<T> : InternalSerializer<VecStorage<T>>
    {
        public override byte KnownTypeId => 0;

        public override short FixedSize => 0;

        public override int SizeOf(in VecStorage<T> value, BufferWriter payload)
        {
            if (!TypeHelper<T>.IsFixedSize)
            {
                ThrowHelper.ThrowNotSupportedException();
            }
            return 4 + value.Storage.Vec.Length * Unsafe.SizeOf<T>();
        }

        public override unsafe int Write(in VecStorage<T> value, DirectBuffer destination)
        {
            if (!TypeHelper<T>.IsFixedSize)
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            // TODO we will use negative length as shuffled flag
            // but will need to add support for ArrayConverter for this.

            destination.Write(0, -value.Storage.Vec.Length); // Minus for shuffled
            if (value.Storage.Vec.Length > 0)
            {
                var byteLen = Unsafe.SizeOf<T>() * value.Storage.Vec.Length;
                Debug.Assert(destination.Length >= 4 + byteLen);

                if (TypeHelper<T>.IsIDelta)
                {
                    // one boxing TODO this is wrong direction, we need i - first, maybe add reverse delta or for binary it's OK?
                    var first = (IDelta<T>)value.Storage.Vec.DangerousGetUnaligned<T>(0);
                    var arr = BufferPool<T>.Rent(value.Storage.Vec.Length);
                    arr[0] = value.Storage.Vec.DangerousGetUnaligned<T>(0);
                    for (int i = 1; i < value.Storage.Vec.Length; i++)
                    {
                        arr[i] = first.GetDelta(value.Storage.Vec.DangerousGetUnaligned<T>(i));
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
                    fixed (byte* sPtr = &Unsafe.As<T, byte>(ref value.Storage.Vec.DangerousGetRef<T>(0)))
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

        public override unsafe int Read(DirectBuffer source, out VecStorage<T> value)
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
                var rm = BufferPool<T>.MemoryPool.RentMemory(arraySize) as ArrayMemory<T>;
                Debug.Assert(rm != null);

                // ReSharper disable once PossibleNullReferenceException
                fixed (byte* dPtr = &Unsafe.As<T, byte>(ref rm.Vec.DangerousGetRef(0)))
                {
                    var srcDb = source.Slice(4).Span;
                    var destDb = new Span<byte>(dPtr, byteLen);

                    // srcDb.CopyTo(destDb);
                    BinarySerializer.Unshuffle(srcDb, destDb, (byte)Unsafe.SizeOf<T>());
                }

                if (TypeHelper<T>.IsIDelta)
                {
                    var first = (IDelta<T>)rm.Array[0];
                    for (int i = 1; i < arraySize; i++)
                    {
                        rm.Array[i] = first.AddDelta(rm.Array[i]);
                    }
                }

                var vs = VecStorage.Create<T>(rm, 0, arraySize);

                value = new VecStorage<T>(vs);
            }
            else
            {
                value = new VecStorage<T>(default);
            }

            return 4 + payloadSize;
        }
    }
}