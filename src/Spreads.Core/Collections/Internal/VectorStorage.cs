// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Native;
using Spreads.Serialization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Collections.Internal
{
    /// <summary>
    /// VectorStorage is a grouping of data and its source, of which it borrows a counted reference.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct VectorStorage : IDisposable, IVector, IEquatable<VectorStorage>
    {
#if DEBUG
        private string StackTrace = Environment.StackTrace;
#endif

        private VectorStorage(IRefCounted memorySource, Vec vec)
        {
            _memorySource = memorySource;
            _vec = vec;
        }

        /// <summary>
        /// A source that owns Vec memory.
        /// </summary>
        /// <remarks>
        /// This is intended to be <see cref="RetainableMemory{T}"/>, but we do not have T here and only care about ref counting.
        /// </remarks>
        internal readonly IRefCounted _memorySource;

        private readonly Vec _vec;

        /// <summary>
        /// Returns new VectorStorage instance with the same memory source but (optionally) different memory start, length and stride.
        /// Increments underlying memory reference count.
        /// </summary>
        /// <returns></returns>
        public VectorStorage Slice(int start,
            int length,
            bool externallyOwned = false)
        {
            var ms = _memorySource;
            var vec = _vec.Slice(start, length);

            if (!externallyOwned)
            {
                ms!.Increment();
            }

            var vs = new VectorStorage(ms, vec);

            // TODO move stride logic elsewhere
            //var numberOfStridesFromZero = vs._vec.Length;
            //if (stride > 1)
            //{
            //    // last full stride could be incomplete but with the current logic we will access only the first element
            //    if (vs._vec.Length - numberOfStridesFromZero * stride > 0)
            //    {
            //        numberOfStridesFromZero++;
            //    }
            //}

            //if (elementLength != -1)
            //{
            //    if ((uint)elementLength < numberOfStridesFromZero)
            //    {
            //        numberOfStridesFromZero = elementLength;
            //    }
            //    else
            //    {
            //        ThrowHelper.ThrowArgumentOutOfRangeException("elementLength");
            //    }
            //}

            //vs._length = numberOfStridesFromZero;

            return vs;
        }

        public static VectorStorage Create<T>(RetainableMemory<T> memorySource,
            int start,
            int length,
            bool externallyOwned = false)
        {
            var ms = memorySource ?? throw new ArgumentNullException(nameof(memorySource));
            var vec = ms.Vec.AsVec().Slice(start, length);

            if (!externallyOwned)
            {
                ms.Increment();
            }

            var vs = new VectorStorage(ms, vec);

            // TODO move stride logic elsewhere
            //vs._stride = stride;

            //var numberOfStridesFromZero = vs._vec.Length / stride;
            //if (stride > 1)
            //{
            //    // last full stride could be incomplete but with the current logic we will access only the first element
            //    if (vs._vec.Length - numberOfStridesFromZero * stride > 0)
            //    {
            //        numberOfStridesFromZero++;
            //    }
            //}

            //if (elementLength != -1)
            //{
            //    if ((uint)elementLength <= numberOfStridesFromZero)
            //    {
            //        numberOfStridesFromZero = elementLength;
            //    }
            //    else
            //    {
            //        ThrowHelper.ThrowArgumentOutOfRangeException("elementLength");
            //    }
            //}

            //vs._length = numberOfStridesFromZero;

            return vs;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vec.Length == 0;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vec.Length;
        }

        public Type Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vec.Type;
        }

        public object this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (unchecked((uint)index) >= unchecked((uint)_vec.Length))
                {
                    VecThrowHelper.ThrowIndexOutOfRangeException();
                }
                return DangerousGet(index);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (unchecked((uint)index) >= unchecked((uint)_vec.Length))
                {
                    VecThrowHelper.ThrowIndexOutOfRangeException();
                }
                DangerousSet(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object DangerousGet(int index)
        {
            return _vec.DangerousGet(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousSet(int index, object value)
        {
            _vec.DangerousSet(index, value);
        }

        [Obsolete("This is slow if the type T knownly matches the underlying type (the method has type check in addition to bound check). Use typed Vector<T> view over VectorStorage.")]
        public T Get<T>(int index)
        {
            return _vec.Get<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGet<T>(int index)
        {
            return _vec.DangerousGet<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index)
        {
            return ref _vec.GetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T DangerousGetRef<T>(int index)
        {
            return ref _vec.DangerousGetRef<T>(index);
        }

        public Vec Vec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _vec;
        }

        public void Dispose()
        {
            _memorySource?.Decrement();
        }

        public bool Equals(VectorStorage other)
        {
            if (ReferenceEquals(_memorySource, other._memorySource)
                && Length == other.Length)
            {
                if (_memorySource != null)
                {
                    return Length == 0 || Unsafe.AreSame(ref DangerousGetRef<byte>(0), ref other.DangerousGetRef<byte>(0));
                }

                return other._memorySource == null;
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return obj is VectorStorage other && Equals(other);
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        public static bool operator ==(VectorStorage left, VectorStorage right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VectorStorage left, VectorStorage right)
        {
            return !(left == right);
        }
    }

    // TODO if we register it similarly to ArrayBinaryConverter only for blittable Ts
    // then we could just use BinarySerializer over the wrapper and it will automatically
    // use JSON and set the right header. We do not want to use JSON inside binary with confusing header.

    internal delegate int SizeOfDelegate(VectorStorage value,
        out (BufferWriter bufferWriter, SerializationFormat format) payload,
        SerializationFormat format = default);

    internal delegate int WriteDelegate(VectorStorage value,
        ref DirectBuffer pinnedDestination,
        in (BufferWriter bufferWriter, SerializationFormat format) payload,
        SerializationFormat format = default);

    internal delegate int ReadDelegate(ref DirectBuffer source, out VectorStorage value);

    internal readonly struct VectorStorage<T>
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
        public VectorStorage(VectorStorage storage)
        {
            if (VecTypeHelper<T>.RuntimeVecInfo.RuntimeTypeId != storage.Vec.RuntimeTypeId)
            {
                VecThrowHelper.ThrowVecTypeMismatchException();
            }
            Storage = storage;
        }

        public readonly VectorStorage Storage;
    }
}
