// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Collections
{
    // This is based on slow Span<T> implementation.
    // 1. Working with untyped/unconstrained generic Vec/Vec<T> is simpler
    //    than using ifs everywhere.
    // 2. There is one if on every path, but if we always use native memory
    //    for blittable types then this if should be predicted perfectly.
    //    Every generic method has it's own compilation for value types
    //    and shared path for reference types, for CPU they are different
    //    branches that are predicted independently.
    // 3. One day Span<T> could be stored as a field of a normal (not ref)
    //    struct and we will only need to change Vec implementation.

    /// <summary>
    /// Typed native or managed vector.
    /// </summary>
    /// <remarks>Not thread safe and not safe at all</remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public readonly unsafe struct Vec<T> : IEnumerable<T>
    {
        //
        // If the Vec was constructed from an object,
        //
        //   _pinnable   = that object (unsafe-casted to a Pinnable<T>)
        //   _byteOffset = offset in bytes from "ref _pinnable.Data" to "ref vec[0]"
        //
        // If the Span was constructed from a native pointer,
        //
        //   _pinnable   = null
        //   _byteOffset = the pointer
        //

        internal readonly Box<T>? _pinnable;
        internal readonly IntPtr _byteOffset;
        private readonly int _length;
        internal readonly RuntimeTypeId _runtimeTypeId; // padded anyway due to obj usage, no additional mem vs portable span

        /// <summary>
        /// Creates a new Vec over the entirety of the target array.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
        /// <exception cref="ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec(T[]? array)
        {
            if (array == null)
            {
                this = default;
                return; // returns default
            }

            if (default(T) is null && array.GetType() != typeof(T[]))
                ThrowHelper.ThrowArrayTypeMismatchException();

            _length = array.Length;
            _pinnable = Unsafe.As<Box<T>>(array);
            _byteOffset = (IntPtr) TypeHelper<T>.ArrayOffset;

            _runtimeTypeId = TypeHelper<T>.RuntimeTypeId;
        }

        // This is a constructor that takes an array and start but not length. The reason we expose it as a static method as a constructor
        // is to mirror the actual api shape. This overload of the constructor was removed from the api surface area due to possible
        // confusion with other overloads that take an int parameter that don't represent a start index.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vec<T> Create(T[]? array, int start)
        {
            if (array == null)
            {
                if (start != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                return default;
            }

            if (default(T) is null && array.GetType() != typeof(T[]))
                ThrowHelper.ThrowArrayTypeMismatchException();
            if (unchecked((uint) start) > unchecked((uint) array.Length))
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            IntPtr byteOffset = (IntPtr) Unsafe.Add<T>((byte*) TypeHelper<T>.ArrayOffset, start);
            int length = array.Length - start;
            return new Vec<T>(pinnable: Unsafe.As<Box<T>>(array), byteOffset: byteOffset, length: length, runtimeTypeId: TypeHelper<T>.RuntimeTypeId);
        }

        /// <summary>
        /// Creates a new Vec over the portion of the target array beginning
        /// at 'start' index and ending at 'end' index (exclusive).
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="start">The index at which to begin the Vec.</param>
        /// <param name="length">The number of items in the Vec.</param>
        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in the range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec(T[]? array, int start, int length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                this = default;
                return; // returns default
            }

            if (default(T) is null && array.GetType() != typeof(T[]))
                ThrowHelper.ThrowArrayTypeMismatchException();

            ThrowHelper.EnsureOffsetLength(start, length, array.Length);

            _length = length;
            _pinnable = Unsafe.As<Box<T>>(array);
            _byteOffset = (IntPtr) Unsafe.Add<T>((byte*) TypeHelper<T>.ArrayOffset, start);
            _runtimeTypeId = TypeHelper<T>.RuntimeTypeId;
        }

        /// <summary>
        /// Creates a new Vec over the target unmanaged buffer.  Clearly this
        /// is quite dangerous, because we are creating arbitrarily typed T's
        /// out of a void*-typed block of memory.  And the length is not checked.
        /// But if this creation is correct, then all subsequent uses are correct.
        /// </summary>
        /// <param name="pointer">An unmanaged pointer to memory.</param>
        /// <param name="length">The number of <typeparamref name="T"/> elements the memory contains.</param>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <typeparamref name="T"/> is reference type or contains pointers and hence cannot be stored in unmanaged memory.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="length"/> is negative.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec(void* pointer, int length)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                ThrowHelper.ThrowInvalidTypeWithPointersNotSupported(typeof(T));

            if (length < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);

            _length = length;
            _pinnable = null;
            _byteOffset = new IntPtr(pointer);
            // negative
            _runtimeTypeId = TypeHelper<T>.RuntimeTypeId;
        }

        // Constructor for internal use only.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vec(Box<T>? pinnable, IntPtr byteOffset, int length, RuntimeTypeId runtimeTypeId)
        {
            Debug.Assert(length >= 0);

            _length = length;
            _pinnable = pinnable;
            _byteOffset = byteOffset;
            _runtimeTypeId = runtimeTypeId;
        }

        /// <summary>
        /// Returns untyped <see cref="Vec"/> with the same data as this <see cref="Vec{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec AsVec()
        {
            return new Vec(Unsafe.As<Array>(_pinnable), _byteOffset, _length, _runtimeTypeId);
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_pinnable == null)
                    return new Span<T>((void*) _byteOffset, _length);

                return new Span<T>(Unsafe.As<T[]>(_pinnable), (int) (checked((uint) _byteOffset - TypeHelper<T>.ArrayOffset)) / Unsafe.SizeOf<T>(), _length);
            }
        }

        internal Span<T> UnsafeSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TypeHelper<T>.IsReferenceOrContainsReferences)
                    return new Span<T>(Unsafe.As<T[]>(_pinnable), (int) (checked((uint) _byteOffset - TypeHelper<T>.ArrayOffset)) / Unsafe.SizeOf<T>(), _length);

                return new Span<T>((void*) _byteOffset, _length);
            }
        }

        /// <summary>
        /// Get the total number of elements in Vec.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Returns true if Vec is created via a pointer. Vec could still be manually pinned if it was created with an array of blittable types.
        /// </summary>
        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnable == null;
        }

        internal int RuntimeTypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _runtimeTypeId;
        }

        /// <summary>
        /// Fetches the element at the specified index.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">
        /// Thrown when the specified index is not in range.
        /// </exception>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (unchecked((uint) index) >= unchecked((uint) _length))
                    ThrowHelper.ThrowIndexOutOfRangeException();

                return DangerousGetUnaligned(index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (unchecked((uint) index) >= unchecked((uint) _length))
                    ThrowHelper.ThrowIndexOutOfRangeException();

                DangerousSetUnaligned(index, value);
            }
        }

        /// <summary>
        /// Returns a reference to a value at index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef(int index)
        {
            if (unchecked((uint) index) >= unchecked((uint) _length))
                ThrowHelper.ThrowIndexOutOfRangeException();

            return ref DangerousGetRef(index);
        }

        /// <summary>
        /// Returns a reference to a value at index without bound checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T DangerousGetRef(int index)
        {
            if (_pinnable == null)
            {
                return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) _byteOffset), index);
            }

            return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref _pinnable.Value, _byteOffset), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGetUnaligned(int index)
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref DangerousGetRef(index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousSetUnaligned(int index, T value)
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref DangerousGetRef(index)), value);
        }

        /// <summary>
        /// See <see cref="Vec.UnsafeGetRef{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T UnsafeGetRef(IntPtr index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref _pinnable!.Value, _byteOffset), index);

            return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) _byteOffset), index);
        }

        /// <summary>
        /// See <see cref="Vec.UnsafeGetRef{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T UnsafeGetUnaligned(IntPtr index)
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref UnsafeGetRef(index)));
        }

        /// <summary>
        /// See <see cref="Vec.UnsafeGetRef{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnsafeSetUnaligned(IntPtr index, T value)
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref UnsafeGetRef(index)), value);
        }

        /// <summary>
        /// Forms a slice out of the given Vec, beginning at 'start'.
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec<T> Slice(int start)
        {
            if ((uint) start > (uint) _length)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            IntPtr newOffset = (IntPtr) Unsafe.Add<T>((byte*) _byteOffset, start);
            int length = _length - start;
            return new Vec<T>(_pinnable, newOffset, length, _runtimeTypeId);
        }

        /// <summary>
        /// Forms a slice out of the given Vec, beginning at 'start', of given length
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice (exclusive).</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec<T> Slice(int start, int length)
        {
            if ((uint) start > (uint) _length || (uint) length > (uint) (_length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            IntPtr newOffset = (IntPtr) Unsafe.Add<T>((byte*) _byteOffset, start);
            return new Vec<T>(_pinnable, newOffset, length, _runtimeTypeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vec<T> DangerousSlice(int start, int length)
        {
            IntPtr newOffset = (IntPtr) Unsafe.Add<T>((byte*) _byteOffset, start);
            return new Vec<T>(_pinnable, newOffset, length, _runtimeTypeId);
        }

        /// <summary>
        /// Copies the contents of this span into a new array.  This heap
        /// allocates, so should generally be avoided, however it is sometimes
        /// necessary to bridge the gap with APIs written in terms of arrays.
        /// </summary>
        public T[] ToArray()
        {
            return Span.ToArray();
        }

        /// <summary>
        /// Returns a reference to the 0th element of the Vec. If the Span is empty, returns null reference.
        /// It can be used for pinning and is required to support the use of Vec within a fixed statement.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetPinnableReference()
        {
            if (_length != 0)
            {
                if (_pinnable == null)
                {
                    return ref Unsafe.AsRef<T>((void*) _byteOffset);
                }

                return ref Unsafe.AddByteOffset(ref _pinnable.Value, _byteOffset);
            }

            return ref Unsafe.AsRef<T>(source: null);
        }

        /// <summary>
        /// This method is obsolete, use System.Runtime.InteropServices.MemoryMarshal.GetReference instead.
        /// Returns a reference to the 0th element of the Span. If the Span is empty, returns a reference to the location where the 0th element
        /// would have been stored. Such a reference can be used for pinning but must never be dereferenced.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal ref T DangerousGetPinnableReference()
        {
            if (_pinnable == null)
            {
                return ref Unsafe.AsRef<T>((void*) _byteOffset);
            }
            else
            {
                return ref Unsafe.AddByteOffset(ref _pinnable.Value, _byteOffset);
            }
        }

        /// <summary>
        /// Clears the contents of this Vec.
        /// </summary>
        public void Clear()
        {
            Span.Clear();
        }

        /// <summary>
        /// Fills the contents of this Vec with the given value.
        /// </summary>
        public void Fill(T value)
        {
            Span.Fill(value);
        }

        internal void FillNonGeneric(object value)
        {
            Span.Fill((T) value);
        }

        /// <summary>
        /// Copies the contents of this Vec into destination Vec. If the source
        /// and destinations overlap, this method behaves as if the original values in
        /// a temporary location before the destination is overwritten.
        ///
        /// <param name="destination">The Vec to copy items into.</param>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the destination Span is shorter than the source Span.
        /// </exception>
        /// </summary>
        public void CopyTo(Vec<T> destination)
        {
            if (!TryCopyTo(destination))
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        /// <summary>
        /// Copies the contents of this Vec into destination Vec. If the source
        /// and destinations overlap, this method behaves as if the original values in
        /// a temporary location before the destination is overwritten.
        ///
        /// <returns>If the destination Vec is shorter than the source Vec, this method
        /// return false and no data is written to the destination.</returns>
        /// </summary>
        /// <param name="destination">The Vec to copy items into.</param>
        public bool TryCopyTo(Vec<T> destination)
        {
            return Span.TryCopyTo(destination.Span);
        }

        /// <summary>
        /// Returns true if left and right point at the same memory and have the same length.  Note that
        /// this does *not* check to see if the *contents* are equal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ReferenceEquals(Vec<T> other)
        {
            return _length == other._length
                   && Unsafe.AreSame(ref DangerousGetPinnableReference(), ref other.DangerousGetPinnableReference())
                   && _runtimeTypeId == other._runtimeTypeId;
        }

        /// <summary>
        /// Returns a <see cref="String"/> with the name of the type and the number of elements.
        /// </summary>
        public override string ToString()
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return $"Spreads.Vec<{typeof(T).Name}>[{_length}]";
        }

        /// <summary>
        /// Returns an enumerator over the Slice's entire contents.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return GetEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return GetEnumerator();
        }

        /// <summary>
        /// A struct-based enumerator, to make fast enumerations possible.
        /// This isn't designed for direct use, instead see GetEnumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private int _position; // The current position.
            private Vec<T> _vec; // The slice being enumerated.

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(Vec<T> vec)
            {
                _vec = vec;
                _position = -1;
            }

            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _vec.DangerousGetUnaligned(_position);
            }

            // ReSharper disable once HeapView.BoxingAllocation
            object IEnumerator.Current => Current!;

            public void Dispose()
            {
                _vec = default;
                _position = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                _position++;
                return _vec._length > _position;
            }

            public void Reset()
            {
                _position = -1;
            }
        }
    }

    /// <summary>
    /// Untyped native or managed vector.
    /// </summary>
    /// <remarks>Not thread safe and not safe at all</remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 24)]
    public readonly unsafe struct Vec : IEnumerable
    {
        [FieldOffset(0)]
        internal readonly Array? _pinnable;
        [FieldOffset(8)]
        internal readonly IntPtr _byteOffset;
        [FieldOffset(16)]
        private readonly int _length;
        [FieldOffset(20)]
        internal readonly RuntimeTypeId _runtimeTypeId;

        /// <summary>
        /// Creates a new Vec over the entirety of the target array.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
        /// <exception cref="ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec(Array? array)
        {
            if (array == null)
            {
                this = default;
                return; // returns default
            }

            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                ThrowHelper.ThrowInvalidOperationException_ArrayNotVector();

            _length = array.Length;
            _pinnable = array;
            _byteOffset = TypeHelper.ArrayOffset;
            _runtimeTypeId = TypeHelper.GetRuntimeTypeInfo(array.GetType().GetElementType()!).RuntimeTypeId;
        }

        // This is a constructor that takes an array and start but not length. The reason we expose it as a static method as a constructor
        // is to mirror the actual api shape. This overload of the constructor was removed from the api surface area due to possible
        // confusion with other overloads that take an int parameter that don't represent a start index.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vec Create(Array? array, int start)
        {
            if (array == null)
            {
                if (start != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
                return default;
            }

            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                ThrowHelper.ThrowInvalidOperationException_ArrayNotVector();

            if (unchecked((uint) start) > unchecked((uint) array.Length))
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            ref readonly var vti = ref TypeHelper.GetRuntimeTypeInfo(array.GetType().GetElementType()!);

            IntPtr byteOffset = TypeHelper.ArrayOffset + start * vti.ElemSize;
            int length = array.Length - start;
            return new Vec(array: array, byteOffset: byteOffset, length: length, runtimeTypeId: vti.RuntimeTypeId);
        }

        /// <summary>
        /// Creates a new Vec over the portion of the target array beginning
        /// at 'start' index and ending at 'end' index (exclusive).
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="start">The index at which to begin the Vec.</param>
        /// <param name="length">The number of items in the Vec.</param>
        /// <remarks>Returns default when <paramref name="array"/> is null.</remarks>
        /// <exception cref="System.ArrayTypeMismatchException">Thrown when <paramref name="array"/> is covariant and array's type is not exactly T[].</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in the range (&lt;0 or &gt;=Length).
        /// </exception>
        public Vec(Array? array, int start, int length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0)
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                this = default;
                return; // returns default
            }

            if (array.Rank != 1 || array.GetLowerBound(0) != 0)
                ThrowHelper.ThrowInvalidOperationException_ArrayNotVector();

            // ReSharper disable once PossibleNullReferenceException
            ref readonly var vti = ref TypeHelper.GetRuntimeTypeInfo(array.GetType().GetElementType()!);

            ThrowHelper.EnsureOffsetLength(start, length, array.Length);

            _length = length;
            _pinnable = array;
            _byteOffset = TypeHelper.ArrayOffset + start * vti.ElemSize;
            _runtimeTypeId = vti.RuntimeTypeId;
        }

        /// <summary>
        /// Creates a new Vec over the target unmanaged buffer.  Clearly this
        /// is quite dangerous, because we are creating arbitrarily typed T's
        /// out of a void*-typed block of memory.  And the length is not checked.
        /// But if this creation is correct, then all subsequent uses are correct.
        /// </summary>
        /// <param name="pointer">An unmanaged pointer to memory.</param>
        /// <param name="length">The number of elements the memory contains.</param>
        /// <param name="elementType">Element type.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="length"/> is negative.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec(void* pointer, int length, Type? elementType)
        {
            if (elementType == null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.elementType);

            ref readonly var vti = ref TypeHelper.GetRuntimeTypeInfo(elementType);

            if (vti.IsReferenceOrContainsReferences)
                ThrowHelper.ThrowInvalidTypeWithPointersNotSupported(elementType);

            if (length < 0)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);

            if (pointer == null && length != 0)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pointer);



            _pinnable = null;
            _byteOffset = (IntPtr) pointer!;
            _length = length;
            _runtimeTypeId = vti.RuntimeTypeId;
        }

        /// <summary>
        /// An internal helper for creating Vecs.  Not for public use.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vec(Array? array, IntPtr byteOffset, int length, RuntimeTypeId runtimeTypeId)
        {
#if DEBUG
            Debug.Assert(array == null || runtimeTypeId == TypeHelper.GetRuntimeTypeInfo(array.GetType().GetElementType()!).RuntimeTypeId);
#endif
            _pinnable = array;
            _byteOffset = byteOffset;
            _length = length;
            _runtimeTypeId = runtimeTypeId;
        }

        /// <summary>
        /// Returns typed <see cref="Vec{T}"/> with the same data as this <see cref="Vec"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec<T> As<T>()
        {
            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId)
                ThrowHelper.ThrowVecRuntimeTypeMismatchException();

            return new Vec<T>(Unsafe.As<Box<T>>(_pinnable), _byteOffset, _length, _runtimeTypeId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan<T>()
        {
            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId)
                ThrowHelper.ThrowVecRuntimeTypeMismatchException();

            if (_pinnable == null)
                return new Span<T>((void*) _byteOffset, _length);

            return new Span<T>(Unsafe.As<T[]>(_pinnable), (int) (checked((uint) _byteOffset - TypeHelper<T>.ArrayOffset)) / Unsafe.SizeOf<T>(), _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> UnsafeAsSpan<T>()
        {
            Debug.Assert(TypeHelper<T>.RuntimeTypeId != _runtimeTypeId);

            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return new Span<T>(Unsafe.As<T[]>(_pinnable), (int) (checked((uint) _byteOffset - TypeHelper<T>.ArrayOffset)) / Unsafe.SizeOf<T>(), _length);

            return new Span<T>((void*) _byteOffset, _length);
        }

        /// <summary>
        /// Get the total number of elements in Vec.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Returns true if Vec is created via a pointer. Vec could still be manually pinned if it was create with an array of blittable types.
        /// </summary>
        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pinnable == null;
        }

        public int RuntimeTypeId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _runtimeTypeId;
        }

        public Type ItemType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TypeHelper.GetRuntimeTypeInfo(_runtimeTypeId).Type;
        }

        /// <summary>
        /// Returns the element at the specified index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId || unchecked((uint) index) >= unchecked((uint) _length))
                ThrowWrongLengthOrType<T>(index);

            return DangerousGetUnaligned<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, T value)
        {
            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId || unchecked((uint) index) >= unchecked((uint) _length))
                ThrowWrongLengthOrType<T>(index);

            DangerousSetUnaligned(index, value);
        }

        /// <summary>
        /// Get a typed reference to a value at index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index)
        {
            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId || unchecked((uint) index) >= unchecked((uint) _length))
                ThrowWrongLengthOrType<T>(index);

            return ref DangerousGetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowWrongLengthOrType<T>(int index)
        {
            if (unchecked((uint) index) >= unchecked((uint) _length))
               ThrowHelper.ThrowIndexOutOfRangeException();

            if (TypeHelper<T>.RuntimeTypeId != _runtimeTypeId)
                ThrowHelper.ThrowInvalidOperationException("Wrong type in object to T conversion: T is " + typeof(T).Name);
        }

        /// <summary>
        /// Get a typed reference to a value at index without type or bounds check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T DangerousGetRef<T>(int index)
        {
            if (_pinnable != null)
                return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(_pinnable)!.Value, _byteOffset), index);

            return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) _byteOffset), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static object? DangerousGetObject<T>(in Vec vec, int index)
        {
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            return vec.DangerousGetRef<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object DangerousGetObject(int index)
        {
            return TypeHelper.GetRuntimeTypeInfo(_runtimeTypeId).DangerousGetObjectDelegate(this, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T DangerousGetUnaligned<T>(int index)
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref DangerousGetRef<T>(index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DangerousSetUnaligned<T>(int index, T value)
        {
            // For underlying pointers it just works as casting pointers/refs is simple.
            // For refs/non-blittable types it's more complicated:
            // `As<T, byte>` is a simple `ret` instruction, so we get
            // ```
            // unaligned. 0x01
            // stobj !!T
            // ```
            // to !!T&
            // ECMA-335 doesn't say anything about that .unaligned could only
            // be applied to value types. "The operation of the stobj
            // instruction can be altered by an immediately preceding unaligned. prefix instruction."
            // This should work fine: https://github.com/dotnet/runtime/issues/1650

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref DangerousGetRef<T>(index)), value);
        }

        /// <summary>
        /// This method assumes that types without references are always backed with pinned/native memory (pointer != null).
        /// The reference check is done via <see cref="TypeHelper{T}.IsReferenceOrContainsReferences"/>.
        /// This check should be JIT-time constant and the performance should be closer to native <see cref="Span{T}"/>.
        /// Dangerous-prefixed methods only skip bounds check while this method is *very unsafe*.
        /// It should be used together with Spreads.Buffers.PrivateMemory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T UnsafeGetRef<T>(IntPtr index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Box<T>>(_pinnable)!.Value, _byteOffset), index);

            return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) _byteOffset), index);
        }

        /// <summary>
        /// See <see cref="Vec.UnsafeGetRef{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T UnsafeGetUnaligned<T>(IntPtr index)
        {
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref UnsafeGetRef<T>(index)));
        }

        /// <summary>
        /// See <see cref="Vec.UnsafeGetRef{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnsafeSetUnaligned<T>(IntPtr index, T value)
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref UnsafeGetRef<T>(index)), value);
        }

        /// <summary>
        /// Forms a slice out of the given Vec, beginning at 'start'.
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start)
        {
            if ((uint) start > (uint) _length)
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);

            IntPtr newOffset = _byteOffset + start * TypeHelper.GetRuntimeTypeInfo(_runtimeTypeId).ElemSize;
            int length = _length - start;
            return new Vec(_pinnable, newOffset, length, _runtimeTypeId);
        }

        /// <summary>
        /// Forms a slice out of the given Vec, beginning at 'start', of given length
        /// </summary>
        /// <param name="start">The index at which to begin this slice.</param>
        /// <param name="length">The desired length for the slice (exclusive).</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;=Length).
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec Slice(int start, int length)
        {
            ThrowHelper.EnsureOffsetLength(start, length, _length);
            IntPtr newOffset = _byteOffset + start * TypeHelper.GetRuntimeTypeInfo(_runtimeTypeId).ElemSize;
            return new Vec(_pinnable, newOffset, length, _runtimeTypeId);
        }

        /// <summary>
        /// Checks to see if two Vecs point at the same memory.  Note that
        /// this does *not* check to see if the *contents* are equal.
        /// </summary>
        public bool ReferenceEquals(Vec other)
        {
            return _pinnable == other._pinnable
                   && _byteOffset == other._byteOffset
                   && _length == other._length
                   && _runtimeTypeId == other._runtimeTypeId;
        }

        /// <summary>
        /// Returns a <see cref="String"/> with the name of the type and the number of elements.
        /// </summary>
        public override string ToString()
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return $"Spreads.Vec[{_length}] of {ItemType.Name}";
        }

        /// <summary>
        /// Returns an enumerator over the Slice's entire contents.
        /// </summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            // ReSharper disable once HeapView.BoxingAllocation
            return GetEnumerator();
        }

        /// <summary>
        /// A struct-based enumerator, to make fast enumerations possible.
        /// This isn't designed for direct use, instead see GetEnumerator.
        /// </summary>
        public struct Enumerator : IEnumerator<object>
        {
            private Vec _vec; // The slice being enumerated.
            private int _position; // The current position.

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(Vec vec)
            {
                _vec = vec;
                _position = -1;
            }

            public object Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _vec.DangerousGetObject(_position);
            }

            object IEnumerator.Current => Current;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _vec = default;
                _position = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                return ++_position < _vec.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Reset()
            {
                _position = -1;
            }
        }
    }
}
