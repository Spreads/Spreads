// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using Spreads.Native;
using Spreads.Serialization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// VecStorage is a grouping of data as <see cref="Vec"/> and its source as <see cref="IRefCounted"/>, of which VecStorage borrows a counted reference.
    /// </summary>
    /// <remarks>
    /// This is a struct that must be disposed after it is no longer used.
    /// The intended usage is that this struct is a field of a class
    /// that implements <seealso cref="IDisposable"/> and calls <see cref="Dispose"/>
    /// method on this struct in its <see cref="IDisposable.Dispose"/>
    /// method. See <see cref="DataBlock"/> implementation as an example.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct VecStorage : IDisposable, IEquatable<VecStorage>
    {
        /// <summary>
        /// A source that owns Vec memory.
        /// </summary>
        /// <remarks>
        /// This is intended to be <see cref="RetainableMemory{T}"/>, but we do not have T here and only care about ref counting.
        /// </remarks>
        internal readonly IRefCounted? _memorySource;

        public readonly Vec Vec;

        private VecStorage(IRefCounted? memorySource, Vec vec)
        {
            _memorySource = memorySource;
            Vec = vec;
        }

        /// <summary>
        /// Returns new VectorStorage instance with the same memory source but (optionally) different memory start and length.
        /// Increments underlying memory reference count.
        /// </summary>
        public VecStorage Slice(int start,
            int length,
            bool externallyOwned = false)
        {
            var ms = _memorySource;
            var vec = Vec.Slice(start, length);

            if (!externallyOwned)
            { ms?.Increment(); }

            var vs = new VecStorage(externallyOwned ? null : ms, vec);

            return vs;
        }

        public static VecStorage Create<T>(RetainableMemory<T> memorySource,
            int start,
            int length,
            bool externallyOwned = false)
        {
            var ms = memorySource ?? throw new ArgumentNullException(nameof(memorySource));
            var vec = ms.Vec.AsVec().Slice(start, length);

            if (!externallyOwned)
            { ms.Increment(); }

            var vs = new VecStorage(externallyOwned ? null : ms, vec);

            return vs;
        }

        /// <summary>
        /// True if this VectorStorage does not own a RefCount of the underlying memory source.
        /// </summary>
        private bool IsExternallyOwned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memorySource == null;
        }

        public void Dispose()
        {
            _memorySource?.Decrement();
        }

        public bool Equals(VecStorage other)
        {
            if (ReferenceEquals(_memorySource, other._memorySource)
                && Vec.Length == other.Vec.Length)
            {
                if (_memorySource != null)
                {
                    return Vec.Length == 0 || Unsafe.AreSame(ref Vec.DangerousGetRef<byte>(0), ref other.Vec.DangerousGetRef<byte>(0));
                }

                return other._memorySource == null;
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return obj is VecStorage other && Equals(other);
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        public static bool operator ==(VecStorage left, VecStorage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VecStorage left, VecStorage right)
        {
            return !(left == right);
        }
    }

    // TODO if we register it similarly to ArrayBinaryConverter only for blittable Ts
    // then we could just use BinarySerializer over the wrapper and it will automatically
    // use JSON and set the right header. We do not want to use JSON inside binary with confusing header.

    internal delegate int SizeOfDelegate(VecStorage value,
        out (BufferWriter bufferWriter, SerializationFormat format) payload,
        SerializationFormat format = default);

    internal delegate int WriteDelegate(VecStorage value,
        ref DirectBuffer pinnedDestination,
        in (BufferWriter bufferWriter, SerializationFormat format) payload,
        SerializationFormat format = default);

    internal delegate int ReadDelegate(ref DirectBuffer source, out VecStorage value);

    internal readonly struct VecStorage<T>
    {
        // ReSharper disable StaticMemberInGenericType

        //public static SizeOfDelegate SizeOfDelegate = SizeOf;

        //public static WriteDelegate WriteDelegate = Write;

        //public static ReadDelegate ReadDelegate = Read;

        //// ReSharper restore StaticMemberInGenericType

        //private static int Read(ref DirectBuffer source, out VectorStorage value)
        //{
        //    var len = BinarySerializer.Read(source, out VectorStorage<T> valueT);
        //    value = valueT.Storage;
        //    return len;
        //}

        //private static int Write(VectorStorage value,
        //    ref DirectBuffer pinnedDestination,
        //    in (BufferWriter bufferWriter, SerializationFormat format) payload,
        //    SerializationFormat format = default)
        //{
        //    return BinarySerializer.Write(new VectorStorage<T>(value), pinnedDestination, in payload, format);
        //}

        //private static int SizeOf(VectorStorage value, out (BufferWriter bufferWriter, SerializationFormat format) payload,
        //    SerializationFormat format = default)
        //{
        //    return BinarySerializer.SizeOf(new VectorStorage<T>(value), out payload, format);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VecStorage(VecStorage storage)
        {
            if (VecTypeHelper<T>.RuntimeVecInfo.RuntimeTypeId != storage.Vec.RuntimeTypeId)
            {
                VecThrowHelper.ThrowVecTypeMismatchException();
            }
            Storage = storage;
        }

        public readonly VecStorage Storage;

        public Vec<T> Vec => Storage.Vec.As<T>();
    }
}
