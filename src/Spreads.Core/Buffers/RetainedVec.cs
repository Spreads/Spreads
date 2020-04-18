// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Native;
using Spreads.Serialization;

namespace Spreads.Buffers
{
    /// <summary>
    /// A borrowing of <see cref="PrivateMemory{T}"/> that owns a reference from it.
    /// </summary>
    /// <remarks>
    /// This struct must be disposed and should only be used as a field of an owning object,
    /// which should implement <seealso cref="IDisposable"/> and have a finalizer directly or via its parents.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal readonly struct RetainedVec : IDisposable, IEquatable<RetainedVec>
    {
        // Some ideas from Spreads.Native.Vec are useful, but here we are more restrictive
        // and always store ref+ types as arrays and other types as off-heap memory.
        // Therefore, we could use JIT-constant ISOCR and work with array or pointer 
        // directly, without abstracting ref T. 

        [FieldOffset(0)]
        internal readonly Array? _array;

        /// <summary>
        /// Item offset in <see cref="_array"/> for ref+ types, or starting pointer for blittable types.
        /// </summary>
        [FieldOffset(8)]
        internal readonly IntPtr _pointerOrOffset;

        [FieldOffset(16)]
        internal readonly int _length;

        [FieldOffset(20)]
        internal readonly int _runtimeTypeId;

        [FieldOffset(24)]
        internal readonly IRefCounted? _memoryOwner;

        private RetainedVec(IRefCounted? memoryOwner, Array? array, IntPtr pointerOrOffset, int length, int runtimeTypeId) : this()
        {
            _memoryOwner = memoryOwner;
            _array = array;
            _pointerOrOffset = pointerOrOffset;
            _length = length;
            _runtimeTypeId = runtimeTypeId;
        }

        public static unsafe RetainedVec Create<T>(RetainableMemory<T> memorySource, int start, int length, bool externallyOwned = false)
        {
            if (!memorySource.IsBlittableOffheap)
                ThrowHelper.ThrowInvalidOperationException("Memory source must have IsBlittableOffheap = true to be used in RetainedVec.");

            RetainedVec vs;
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
            {
                ThrowHelper.DebugAssert(memorySource.Pointer == default && memorySource._array != default);
                // RM's offset goes to _pointerOrOffset
                vs = new RetainedVec(externallyOwned ? null : memorySource,
                    memorySource._array,
                    (IntPtr) memorySource._offset,
                    memorySource.Length,
                    VecTypeHelper<T>.RuntimeTypeId);
            }
            else
            {
                ThrowHelper.DebugAssert(memorySource.Pointer != default && memorySource._array == default);
                // RM's offset added to _pointerOrOffset
                vs = new RetainedVec(externallyOwned ? null : memorySource, array: null, (IntPtr) Unsafe.Add<T>(memorySource.Pointer, memorySource._offset), memorySource.Length,
                    VecTypeHelper<T>.RuntimeTypeId);
            }

            return vs.Clone(start, length, externallyOwned);
        }

        public void Dispose()
        {
            _memoryOwner?.Decrement();
        }

        public RetainedVec Clone()
        {
            _memoryOwner?.Increment();
            return new RetainedVec(_memoryOwner, _array, _pointerOrOffset, _length, _runtimeTypeId);
        }

        // TODO WTF is this externallyOwned on slice? See RdM Clone
        /// <summary>
        /// Returns new VectorStorage instance with the same memory source but (optionally) different memory start and length.
        /// Increments underlying memory reference count unless <paramref name="externallyOwned"/> is true.
        /// </summary>
        public RetainedVec Clone(int start, int length, bool externallyOwned = false)
        {
            // see CLR Span.Slice comment
            if (IntPtr.Size == 8)
            {
                if ((ulong) (uint) start + (ulong) (uint) length > (ulong) (uint) _length)
                    ThrowHelper.ThrowArgumentOutOfRangeException();
            }
            else
            {
                if ((uint) start > (uint) _length || (uint) length > (uint) (_length - start))
                    ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            if (!externallyOwned)
                _memoryOwner?.Increment();

            var slice = new RetainedVec(
                externallyOwned ? null : _memoryOwner,
                _array,
                UnsafeEx.Add(_pointerOrOffset,
                    _array == null
                        ? start * VecTypeHelper.GetInfo(_runtimeTypeId).ElemSize
                        : start),
                length,
                _runtimeTypeId
            );

            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe ref T UnsafeGetRef<T>()
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.As<T[]>(_array)[(int) _pointerOrOffset];
            return ref Unsafe.AsRef<T>((void*) _pointerOrOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe ref T UnsafeGetRef<T>(int index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.As<T[]>(_array)[(int) (_pointerOrOffset + index)];
            return ref Unsafe.Add<T>(ref Unsafe.AsRef<T>((void*) _pointerOrOffset), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T UnsafeReadUnaligned<T>(int index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return Unsafe.As<T[]>(_array)[(int) (_pointerOrOffset + index)];
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.AsRef<T>((void*) (_pointerOrOffset + index * Unsafe.SizeOf<T>()))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void UnsafeWriteUnaligned<T>(int index, T value)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                Unsafe.As<T[]>(_array)[(int) UnsafeEx.Add(_pointerOrOffset, index)] = value;
            else
                Unsafe.WriteUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.AsRef<T>((void*) (_pointerOrOffset + index * Unsafe.SizeOf<T>()))), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlySpan<T> UnsafeReadUnaligned<T>(int index, int length)
        {
            return GetSpan<T>().Slice(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnsafeWriteUnaligned<T>(int index, ReadOnlySpan<T> value)
        {
            value.CopyTo(GetSpan<T>().Slice(index));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe Span<T> GetSpan<T>()
        {
            ThrowHelper.DebugAssert(VecTypeHelper<T>.RuntimeTypeId == _runtimeTypeId, "RetainedVec.GetSpan: VecTypeHelper<T>.RuntimeTypeId == _runtimeTypeId");
            return TypeHelper<T>.IsReferenceOrContainsReferences
                ? new Span<T>(Unsafe.As<T[]>(_array), (int) _pointerOrOffset, _length)
                : new Span<T>((void*) _pointerOrOffset, _length * Unsafe.SizeOf<T>());
        }

        /// <summary>
        /// True if this VectorStorage does not own a RefCount of the underlying memory source.
        /// </summary>
        internal bool IsExternallyOwned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memoryOwner == null;
        }

        public int Length => _length;

        public int RuntimeTypeId => _runtimeTypeId;

        public bool Equals(RetainedVec other)
        {
            return _length == other._length
                   && _array == other._array
                   && _pointerOrOffset == other._pointerOrOffset
                // TODO review how we use this equality, a test assumed we compare only content
                // && _memoryOwner == other._memoryOwner
                ;
        }

        public override bool Equals(object obj)
        {
            return obj is RetainedVec other && Equals(other);
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }

        public static bool operator ==(RetainedVec left, RetainedVec right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RetainedVec left, RetainedVec right)
        {
            return !left.Equals(right);
        }
    }

    internal readonly struct RetainedVec<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedVec(RetainedVec retainedVec)
        {
            if (VecTypeHelper<T>.RuntimeTypeId != retainedVec._runtimeTypeId)
                VecThrowHelper.ThrowVecTypeMismatchException();
            Storage = retainedVec;
        }

        public readonly RetainedVec Storage;
    }
}