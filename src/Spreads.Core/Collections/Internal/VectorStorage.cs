// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using Spreads.Native;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Collections.Internal
{
    // **Ownership rules**
    // * Vector storage is owned by DataStorage
    // * Matrix: DataStorage could create multiple VS columns over the same memory,
    //   but in that case there must be DS._values that owns a reference to memory
    //   and the columns MUST NOT increment increment RC for that memory
    // * If DS columns point to different memory then DS._values must be null and
    //   each column owns its reference.
    // * VS owns a reference when its _handle field is not default. It's OK to call Free on default to simplify disposal implementation.

    // TODO comments are just thoughts, most relevant at the bottom, work in progress

    // Design: this may be a struct if we could handle ownership, which
    // implies that is it always owned by another container.
    // Structural sharing: IPinnable with Unpin in dispose
    // Every vector owns a reference to memory manager

    // A. This could be a publicly immutable struct with internal mutability methods and internal Dispose(Unpin/Return) method.
    // Then we could return it as a row of matrices/frames

    // Vec/Vec<T> is logical representation of continuous storage.
    // Vector/Vector<T> is logical vector. It could be continuous or not.

    //[StructLayout(LayoutKind.Sequential)]
    //internal class Vector<T> : VectorStorage
    //{
    //    // no storage in typed Vector

    //    public Vector(RetainableMemory<T> memoryManager, int start, int length)
    //    {
    //        _source = memoryManager;
    //        // store the handle as a field
    //        var handle = _source.Pin(start);

    //        if (MemoryMarshal.TryGetArray<T>(memoryManager.Memory, out var segment))
    //        {
    //            var vec = new Vec<T>(segment.Array, segment.Offset, segment.Count);
    //        }
    //    }
    //}

    /// <summary>
    /// VectorStorage is logical representation of data and its source, of which it borrows a counted reference.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class VectorStorage : IDisposable, IVector
    {
        // TODO (review): this could be made a struct if it is always owned by a class container
#if DEBUG
        private string StackTrace = Environment.StackTrace;
#endif

        public static readonly VectorStorage Empty = new VectorStorage();

        private static readonly ObjectPool<VectorStorage> ObjectPool = new ObjectPool<VectorStorage>(() => new VectorStorage(), Environment.ProcessorCount * 16);

        private VectorStorage()
        { }

        /// <summary>
        /// A source that owns Vec memory.
        /// </summary>
        /// <remarks>
        /// This is intended to be <see cref="RetainableMemory{T}"/>, but we do not have T here and only care about ref counting.
        /// </remarks>
        internal IRefCounted? _memorySource;

        private Vec _vec;

        /// <summary>
        /// Returns new VectorStorage instance with the same memory source but (optionally) different memory start, length and stride.
        /// Increments underlying memory reference count.
        /// </summary>
        /// <returns></returns>
        public VectorStorage Slice(int start,
            int length,
            bool externallyOwned = false)
        {
            var vs = ObjectPool.Allocate();

            vs._memorySource = _memorySource;

            if (!externallyOwned)
            {
                vs._memorySource!.Increment();
            }

            vs._vec = _vec.Slice(start, length);

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
            var vs = ObjectPool.Allocate();

            vs._memorySource = memorySource ?? throw new ArgumentNullException(nameof(memorySource));

            if (!externallyOwned)
            {
                vs._memorySource.Increment();
            }

            vs._vec = memorySource.Vec.AsVec().Slice(start, length);

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

        #region Dispose logic

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memorySource == null;
        }

        private void Dispose(bool disposing)
        {
            var ms = _memorySource;
            if (ms == null) // TODO Empty should have special _memorySource, allocated VSs should never have null ms.
            {
                Debug.Assert(ReferenceEquals(this, Empty));
                return;
            }
            lock (ms)
            {
                if (!disposing)
                {
                    WarnFinalizing();
                }
                if (IsDisposed)
                {
                    ThrowDisposed();
                }
                ms.Decrement();
                _memorySource = null;
            }
            // now we do not care about _source, it is either borrowed by other VectorStorage instances or returned to a pool/GC

            // clear all fields before pooling
            _vec = default;

            ObjectPool.Free(this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void WarnFinalizing()
        {
            Trace.TraceWarning("Finalizing VectorStorage. It must be properly disposed. \n "); // + StackTrace);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposed()
        {
            ThrowHelper.ThrowObjectDisposedException("source");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // TODO need high-load test to detect if there is impact even with correct usage (when always disposing explicitly)
        // VS is owned by DataBlockStorage
        ~VectorStorage()
        {
#if DEBUG
            Trace.TraceWarning($"Finalizing VectorStorage: {StackTrace}");
#endif
            Dispose(false);
        }

        #endregion Dispose logic
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
