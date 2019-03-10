// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    internal static class ArraySerializerFactory
    {
        public static BinarySerializer<TElement[]> GenericCreate<TElement>()
        {
            return new ArraySerializer<TElement>();
        }

        public static object Create(Type type)
        {
            var method = typeof(ArraySerializerFactory).GetTypeInfo().GetMethod(nameof(GenericCreate));
            var generic = method?.MakeGenericMethod(type);
            return generic?.Invoke(null, null);
        }
    }

    /// <summary>
    /// Simple copy of blittable array data. No shuffle.
    /// </summary>
    internal class ArraySerializer<TElement> : BinarySerializer<TElement[]>
    {
        internal static ArraySerializer<TElement> Instance =
            new ArraySerializer<TElement>();

        // This is special, TypeHelper is aware of it (for others version must be > 0)
        public override byte SerializerVersion
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0;
        }

        public override byte KnownTypeId => 0;

        public override short FixedSize => 0;

        public override int SizeOf(in TElement[] value, out RetainedMemory<byte> temporaryBuffer)
        {
            temporaryBuffer = default;
            return 4 + value.Length * SizeOf<TElement>();
        }

        public override int SizeOf(in TElement[] value, BufferWriter bufferWriter)
        {
            throw new NotImplementedException();
        }

        public override unsafe int Write(in TElement[] value, DirectBuffer destination)
        {
            destination.Write(0, value.Length);
            if (value.Length > 0)
            {
                ref var srcRef = ref As<TElement, byte>(ref value[0]);
                ref var dstRef = ref AsRef<byte>(destination.Data + 4);
                var len = SizeOf<TElement>() * value.Length;
                CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)len));
                return 4 + len;
            }
            return 4;
        }

        public override unsafe int Read(DirectBuffer source, out TElement[] value)
        {
            var arraySize = source.Read<int>(0);
            var position = 4;
            var payloadSize = arraySize * SizeOf<TElement>();
            if (4 + payloadSize > source.Length || arraySize < 0)
            {
                value = default;
                return -1;
            }

            if (arraySize > 0)
            {
                TElement[] array;
                if (BitUtil.IsPowerOfTwo(arraySize))
                {
                    array = BufferPool<TElement>.Rent(arraySize);
                    if (array.Length != arraySize)
                    {
                        BufferPool<TElement>.Return(array);
                        array = new TElement[arraySize];
                    }
                }
                else
                {
                    array = new TElement[arraySize];
                }

                ref var dstRef = ref As<TElement, byte>(ref array[0]);
                ref var srcRef = ref AsRef<byte>(source.Data + position);

                CopyBlockUnaligned(ref dstRef, ref srcRef, checked((uint)payloadSize));

                value = array;
            }
            else
            {
                value = EmptyArray<TElement>.Instance;
            }

            return 4 + payloadSize;
        }
    }
}
