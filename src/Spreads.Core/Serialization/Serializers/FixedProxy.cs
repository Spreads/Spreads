// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Serialization.Serializers
{
    /// <summary>
    /// Implements <see cref="BinarySerializer{T}"/> methods as static ones.
    /// If <see cref="BinarySerializer{T}"/> exists then uses it or writes
    /// a <typeparamref name="T"/> directly, in which case it must be an
    /// unmanaged struct.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal static class FixedProxy<T>
    {
        // ReSharper disable once UnusedMember.Local
        private static readonly bool IsValid =
            TypeEnumHelper<T>.IsFixedSize ? true : throw new InvalidOperationException("FixedProxy could only be used for fixed size types.");

        // private static readonly BinarySerializer<T> Serializer = TypeHelper<T>.TypeSerializer;

        internal static short FixedSize = TypeHelper<T>.TypeSerializer?.FixedSize ?? TypeEnumHelper<T>.FixedSize;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf(in T value, BufferWriter bufferWriter)
        {
            if (!TypeHelper<T>.HasTypeSerializer)
            {
                Debug.Assert(bufferWriter == null);
                Debug.Assert(TypeHelper<T>.FixedSize == FixedSize);
                bufferWriter?.Write(in value);
                return FixedSize;
            }
            var sizeOf = TypeHelper<T>.TypeSerializer.SizeOf(in value, bufferWriter);
            
            Debug.Assert(sizeOf == FixedSize);
            return sizeOf;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(in T value, DirectBuffer destination)
        {
            if (!TypeHelper<T>.HasTypeSerializer)
            {
                Debug.Assert(TypeHelper<T>.FixedSize == FixedSize);
                destination.Write(0, value);
                return FixedSize;
            }
            var written = TypeHelper<T>.TypeSerializer.Write(in value, destination);
            Debug.Assert(written == FixedSize);
            return written;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(DirectBuffer source, out T value)
        {
            if (!TypeHelper<T>.HasTypeSerializer)
            {
                Debug.Assert(TypeHelper<T>.PinnedSize == FixedSize);
                value = source.Read<T>(0);
                return FixedSize;
            }
            var consumed = TypeHelper<T>.TypeSerializer.Read(source, out value);
            Debug.Assert(consumed == FixedSize);
            return consumed;
        }
    }
}