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
    /// A borrowing of <see cref="PrivateMemory{T}"/> that owns a reference from it.
    /// </summary>
    /// <remarks>
    /// This struct must be disposed and should only be used as a field of an owning object,
    /// which should implement <seealso cref="IDisposable"/> and have a finalizer directly or via its parents.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal readonly struct RetainedVec : IDisposable, IEquatable<RetainedVec>
    {
        /// <summary>
        /// A source that owns Vec memory.
        /// </summary>
        /// <remarks>
        /// This is intended to be <see cref="RetainableMemory{T}"/>, but we do not have T here and only care about ref counting.
        /// </remarks>
        [FieldOffset(0)]
        internal readonly Vec Vec;

        [FieldOffset(0)]
        internal readonly Array _pinnable;

        [FieldOffset(8)]
        internal readonly IntPtr _byteOffset; // TODO(!) for refs this is static readonly!

        [FieldOffset(16)]
        internal readonly int _length;

        [FieldOffset(20)]
        internal readonly int _runtimeTypeId;

        [FieldOffset(24)]
        internal readonly IRefCounted? _memoryOwner;

        private RetainedVec(IRefCounted? memoryOwner, Vec vec) : this()
        {
            _memoryOwner = memoryOwner;
            Vec = vec;
        }

        public static RetainedVec Create<T>(RetainableMemory<T>? memorySource, int start, int length, bool externallyOwned = false)
        {
            if (!memorySource.IsBlittableOffheap)
                ThrowHelper.ThrowInvalidOperationException("Memory source must have IsBlittableOffheap = true to be used in RetainedVec.");
            var ms = memorySource ?? throw new ArgumentNullException(nameof(memorySource));
            var vec = ms.GetVec().AsVec().Slice(start, length);

            if (!externallyOwned)
                ms.Increment();

            var vs = new RetainedVec(externallyOwned ? null : ms, vec);

            return vs;
        }

        public void Dispose()
        {
            _memoryOwner?.Decrement();
        }

        // TODO WTF is this externallyOwned on slice? See RdM Clone
        /// <summary>
        /// Returns new VectorStorage instance with the same memory source but (optionally) different memory start and length.
        /// Increments underlying memory reference count.
        /// </summary>
        public RetainedVec Slice(int start, int length, bool externallyOwned = false)
        {
            var ms = _memoryOwner;
            var vec = Vec.Slice(start, length);

            if (!externallyOwned)
                ms?.Increment();

            var vs = new RetainedVec(externallyOwned ? null : ms, vec);

            return vs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe ref T UnsafeGetRef<T>(IntPtr index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.Add(
                    ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(_pinnable).Data, _byteOffset),
                    index);
            return ref Unsafe.Add<T>(ref Unsafe.AsRef<T>((void*) _byteOffset), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe T UnsafeReadUnaligned<T>(IntPtr index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add(
                    ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(_pinnable).Data, _byteOffset),
                    index)));
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) _byteOffset), index)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void UnsafeWriteUnaligned<T>(IntPtr index, T value)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                Unsafe.WriteUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add(
                    ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(_pinnable).Data, _byteOffset),
                    index)), value);
            Unsafe.WriteUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add<T>(ref Unsafe.AsRef<T>((void*) _byteOffset), index)), value);
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

        public bool Equals(RetainedVec other)
        {
            if (ReferenceEquals(_memoryOwner, other._memoryOwner)
                && Vec.Length == other.Vec.Length)
            {
                if (_memoryOwner != null)
                    return Vec.Length == 0 || Unsafe.AreSame(ref Vec.DangerousGetRef<byte>(0), ref other.Vec.DangerousGetRef<byte>(0));

                return other._memoryOwner == null;
            }

            return false;
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
        public RetainedVec(RetainedVec storage)
        {
            if (VecTypeHelper<T>.RuntimeTypeId != storage.Vec.RuntimeTypeId)
            {
                VecThrowHelper.ThrowVecTypeMismatchException();
            }

            Storage = storage;
        }

        public readonly RetainedVec Storage;

        public Vec<T> Vec => Storage.Vec.As<T>();
    }
}