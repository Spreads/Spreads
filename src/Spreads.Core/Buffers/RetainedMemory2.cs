//// Licensed to the .NET Foundation under one or more agreements.
//// The .NET Foundation licenses this file to you under the MIT license.
//// See the LICENSE file in the project root for more information.

//using System;
//using System.Buffers;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using Spreads;
//using EditorBrowsableAttribute = System.ComponentModel.EditorBrowsableAttribute;
//using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;

//namespace Exprerimental
//{
    
//    [DebuggerDisplay("{ToString(),raw}")]
//    public unsafe readonly struct Memory2<T>
//    {
//        // NOTE: With the current implementation, Memory<T> and ReadOnlyMemory<T> must have the same layout,
//        // as code uses Unsafe.As to cast between them.

//        // The highest order bit of _index is used to discern whether _object is an array/string or an owned memory
//        // if (_index >> 31) == 1, object _object is an MemoryManager<T>
//        // else, object _object is a T[] or a string.
//        //     if (_length >> 31) == 1, _object is a pre-pinned array, so Pin() will not allocate a new GCHandle
//        //     else, Pin() needs to allocate a new GCHandle to pin the object.
//        // It can only be a string if the Memory<T> was created by
//        // using unsafe / marshaling code to reinterpret a ReadOnlyMemory<char> wrapped around a string as
//        // a Memory<T>.
//        private readonly object _object;
//        private readonly int _index;
//        private readonly int _length;

//        private readonly void* _pointer;
//        private readonly MemoryHandle _handle;

//        // Pointer
//        // GcHandle
//        // IPinnable

//        private const int RemoveFlagsBitMask = 0x7FFFFFFF;

//        /// <summary>
//        /// Creates a new memory over the entirety of the target array.
//        /// </summary>
//        /// <param name="array">The target array.</param>
//        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
//        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public Memory2(T[] array)
//        {
//            if (array == null)
//            {
//                this = default;
//                return; // returns default
//            }
//            if (default(T) == null && array.GetType() != typeof(T[]))
//                ThrowHelper.ThrowInvalidOperationException();

//            _object = array;
//            _index = 0;
//            _length = array.Length;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal Memory2(T[] array, int start)
//        {
//            if (array == null)
//            {
//                if (start != 0)
//                    ThrowHelper.ThrowArgumentOutOfRangeException();
//                this = default;
//                return; // returns default
//            }
//            if (default(T) == null && array.GetType() != typeof(T[]))
//                ThrowHelper.ThrowInvalidOperationException();
//            if ((uint)start > (uint)array.Length)
//                ThrowHelper.ThrowArgumentOutOfRangeException();

//            _object = array;
//            _index = start;
//            _length = array.Length - start;
//        }

//        /// <summary>
//        /// Creates a new memory over the portion of the target array beginning
//        /// at 'start' index and ending at 'end' index (exclusive).
//        /// </summary>
//        /// <param name="array">The target array.</param>
//        /// <param name="start">The index at which to begin the memory.</param>
//        /// <param name="length">The number of items in the memory.</param>
//        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
//        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
//        /// <exception cref="System.ArgumentOutOfRangeException">
//        /// Thrown when the specified <paramref name="start"/> or end index is not in the range (&lt;0 or &gt;=Length).
//        /// </exception>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public Memory2(T[] array, int start, int length)
//        {
//            if (array == null)
//            {
//                if (start != 0 || length != 0)
//                    ThrowHelper.ThrowArgumentOutOfRangeException();
//                this = default;
//                return; // returns default
//            }
//            if (default(T) == null && array.GetType() != typeof(T[]))
//                ThrowHelper.ThrowInvalidOperationException();
//            if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
//                ThrowHelper.ThrowArgumentOutOfRangeException();

//            _object = array;
//            _index = start;
//            _length = length;
//        }

//        /// <summary>
//        /// Creates a new memory from a memory manager that provides specific method implementations beginning
//        /// at 0 index and ending at 'end' index (exclusive).
//        /// </summary>
//        /// <param name="manager">The memory manager.</param>
//        /// <param name="length">The number of items in the memory.</param>
//        /// <exception cref="System.ArgumentOutOfRangeException">
//        /// Thrown when the specified <paramref name="length"/> is negative.
//        /// </exception>
//        /// <remarks>For internal infrastructure only</remarks>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal Memory2(MemoryManager<T> manager, int length)
//        {
//            Debug.Assert(manager != null);

//            if (length < 0)
//                ThrowHelper.ThrowArgumentOutOfRangeException();

//            _object = manager;
//            _index = (1 << 31); // Mark as MemoryManager type
//            // Before using _index, check if _index < 0, then 'and' it with RemoveFlagsBitMask
//            _length = length;
//        }

//        /// <summary>
//        /// Creates a new memory from a memory manager that provides specific method implementations beginning
//        /// at 'start' index and ending at 'end' index (exclusive).
//        /// </summary>
//        /// <param name="manager">The memory manager.</param>
//        /// <param name="start">The index at which to begin the memory.</param>
//        /// <param name="length">The number of items in the memory.</param>
//        /// <exception cref="System.ArgumentOutOfRangeException">
//        /// Thrown when the specified <paramref name="start"/> or <paramref name="length"/> is negative.
//        /// </exception>
//        /// <remarks>For internal infrastructure only</remarks>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal Memory2(MemoryManager<T> manager, int start, int length)
//        {
//            Debug.Assert(manager != null);

//            if (length < 0 || start < 0)
//                ThrowHelper.ThrowArgumentOutOfRangeException();

//            _object = manager;
//            _index = start | (1 << 31); // Mark as MemoryManager type
//            // Before using _index, check if _index < 0, then 'and' it with RemoveFlagsBitMask
//            _length = length;
//        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        internal Memory2(object obj, int start, int length)
//        {
//            // No validation performed; caller must provide any necessary validation.
//            _object = obj;
//            _index = start;
//            _length = length;
//        }

//        /// <summary>
//        /// Defines an implicit conversion of an array to a <see cref="Memory2{T}"/>
//        /// </summary>
//        public static implicit operator Memory2<T>(T[] array) => new Memory2<T>(array);

//        /// <summary>
//        /// Defines an implicit conversion of a <see cref="ArraySegment{T}"/> to a <see cref="Memory2{T}"/>
//        /// </summary>
//        public static implicit operator Memory2<T>(ArraySegment<T> segment) => new Memory2<T>(segment.Array, segment.Offset, segment.Count);

//        /// <summary>
//        /// Defines an implicit conversion of a <see cref="Memory2{T}"/> to a <see cref="ReadOnlyMemory{T}"/>
//        /// </summary>
//        public static implicit operator ReadOnlyMemory<T>(Memory2<T> memory2) =>
//            Unsafe.As<Memory2<T>, ReadOnlyMemory<T>>(ref memory2);

//        /// <summary>
//        /// Returns an empty <see cref="Memory2{T}"/>
//        /// </summary>
//        public static Memory2<T> Empty => default;

//        /// <summary>
//        /// The number of items in the memory.
//        /// </summary>
//        public int Length => _length & RemoveFlagsBitMask;

//        /// <summary>
//        /// Returns true if Length is 0.
//        /// </summary>
//        public bool IsEmpty => (_length & RemoveFlagsBitMask) == 0;

//        /// <summary>
//        /// For <see cref="Memory2{T}"/>, returns a new instance of string that represents the characters pointed to by the memory.
//        /// Otherwise, returns a <see cref="string"/> with the name of the type and the number of elements.
//        /// </summary>
//        public override string ToString()
//        {
//            if (typeof(T) == typeof(char))
//            {
//                return (_object is string str) ? str.Substring(_index, _length & RemoveFlagsBitMask) : Span.ToString();
//            }
//            return string.Format("System.Memory<{0}>[{1}]", typeof(T).Name, _length & RemoveFlagsBitMask);
//        }

//        /// <summary>
//        /// Forms a slice out of the given memory, beginning at 'start'.
//        /// </summary>
//        /// <param name="start">The index at which to begin this slice.</param>
//        /// <exception cref="System.ArgumentOutOfRangeException">
//        /// Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;=Length).
//        /// </exception>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public Memory2<T> Slice(int start)
//        {
//            // Used to maintain the high-bit which indicates whether the Memory has been pre-pinned or not.
//            int capturedLength = _length;
//            int actualLength = capturedLength & RemoveFlagsBitMask;
//            if ((uint)start > (uint)actualLength)
//            {
//                ThrowHelper.ThrowArgumentOutOfRangeException("start");
//            }

//            // It is expected for (capturedLength - start) to be negative if the memory is already pre-pinned.
//            return new Memory2<T>(_object, _index + start, capturedLength - start);
//        }

//        /// <summary>
//        /// Forms a slice out of the given memory, beginning at 'start', of given length
//        /// </summary>
//        /// <param name="start">The index at which to begin this slice.</param>
//        /// <param name="length">The desired length for the slice (exclusive).</param>
//        /// <exception cref="System.ArgumentOutOfRangeException">
//        /// Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;=Length).
//        /// </exception>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public Memory2<T> Slice(int start, int length)
//        {
//            // Used to maintain the high-bit which indicates whether the Memory has been pre-pinned or not.
//            int capturedLength = _length;
//            int actualLength = capturedLength & RemoveFlagsBitMask;
//            if ((uint)start > (uint)actualLength || (uint)length > (uint)(actualLength - start))
//            {
//                ThrowHelper.ThrowArgumentOutOfRangeException();
//            }

//            // Set the high-bit to match the this._length high bit (1 for pre-pinned, 0 for unpinned).
//            return new Memory2<T>(_object, _index + start, length | (capturedLength & ~RemoveFlagsBitMask));
//        }

//        /// <summary>
//        /// Returns a span from the memory.
//        /// </summary>
//        public Span<T> Span
//        {
//            [MethodImpl(MethodImplOptions.AggressiveInlining)]
//            get
//            {
//                if (_index < 0)
//                {
//                    Debug.Assert(_length >= 0);
//                    Debug.Assert(_object != null);
//                    return ((MemoryManager<T>)_object).GetSpan().Slice(_index & RemoveFlagsBitMask, _length);
//                }
//                else if (_object != null)
//                {
//                    return new Span<T>((T[])_object, _index, _length & RemoveFlagsBitMask);
//                }
//                else
//                {
//                    return default;
//                }
//            }
//        }

//        /// <summary>
//        /// Copies the contents of the memory into the destination. If the source
//        /// and destination overlap, this method behaves as if the original values are in
//        /// a temporary location before the destination is overwritten.
//        ///
//        /// <param name="destination">The Memory to copy items into.</param>
//        /// <exception cref="System.ArgumentException">
//        /// Thrown when the destination is shorter than the source.
//        /// </exception>
//        /// </summary>
//        public void CopyTo(Memory2<T> destination) => Span.CopyTo(destination.Span);

//        /// <summary>
//        /// Copies the contents of the memory into the destination. If the source
//        /// and destination overlap, this method behaves as if the original values are in
//        /// a temporary location before the destination is overwritten.
//        ///
//        /// <returns>If the destination is shorter than the source, this method
//        /// return false and no data is written to the destination.</returns>
//        /// </summary>
//        /// <param name="destination">The span to copy items into.</param>
//        public bool TryCopyTo(Memory2<T> destination) => Span.TryCopyTo(destination.Span);

//        /// <summary>
//        /// Creates a handle for the memory.
//        /// The GC will not move the memory until the returned <see cref="MemoryHandle"/>
//        /// is disposed, enabling taking and using the memory's address.
//        /// <exception cref="System.ArgumentException">
//        /// An instance with nonprimitive (non-blittable) members cannot be pinned.
//        /// </exception>
//        /// </summary>
//        public unsafe MemoryHandle Pin()
//        {
//            if (_index < 0)
//            {
//                Debug.Assert(_object != null);
//                return ((MemoryManager<T>)_object).Pin((_index & RemoveFlagsBitMask));
//            }
            
//            else if (_object is T[] array)
//            {
//                // Array is already pre-pinned
//                if (_length < 0)
//                {
//                    void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref array[0]), _index);
//                    return new MemoryHandle(pointer);
//                }
//                else
//                {
//                    GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
//                    void* pointer = Unsafe.Add<T>(Unsafe.AsPointer(ref array[0]), _index);
//                    return new MemoryHandle(pointer, handle);
//                }
//            }
//            return default;
//        }

//        /// <summary>
//        /// Copies the contents from the memory into a new array.  This heap
//        /// allocates, so should generally be avoided, however it is sometimes
//        /// necessary to bridge the gap with APIs written in terms of arrays.
//        /// </summary>
//        public T[] ToArray() => Span.ToArray();

//        /// <summary>
//        /// Determines whether the specified object is equal to the current object.
//        /// Returns true if the object is Memory or ReadOnlyMemory and if both objects point to the same array and have the same length.
//        /// </summary>
//        [EditorBrowsable(EditorBrowsableState.Never)]
//        public override bool Equals(object obj)
//        {
//            if (obj is ReadOnlyMemory<T>)
//            {
//                return ((ReadOnlyMemory<T>)obj).Equals(this);
//            }
//            else if (obj is Memory2<T> memory)
//            {
//                return Equals(memory);
//            }
//            else
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// Returns true if the memory points to the same array and has the same length.  Note that
//        /// this does *not* check to see if the *contents* are equal.
//        /// </summary>
//        public bool Equals(Memory2<T> other)
//        {
//            return
//                _object == other._object &&
//                _index == other._index &&
//                _length == other._length;
//        }

//        /// <summary>
//        /// Serves as the default hash function.
//        /// </summary>
//        [EditorBrowsable(EditorBrowsableState.Never)]
//        public override int GetHashCode()
//        {
//            return _object != null ? CombineHashCodes(_object.GetHashCode(), _index.GetHashCode(), _length.GetHashCode()) : 0;
//        }

//        private static int CombineHashCodes(int left, int right)
//        {
//            return ((left << 5) + left) ^ right;
//        }

//        private static int CombineHashCodes(int h1, int h2, int h3)
//        {
//            return CombineHashCodes(CombineHashCodes(h1, h2), h3);
//        }

//    }
//}