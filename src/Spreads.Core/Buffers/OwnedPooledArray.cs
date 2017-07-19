// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spreads.Buffers
{
    internal sealed class OwnedPooledArray<T> : OwnedBuffer<T>
    {
        // TODO Object pool!
        // ReSharper disable once StaticMemberInGenericType
        private static readonly BoundedConcurrentBag<OwnedPooledArray<T>> Pool = new BoundedConcurrentBag<OwnedPooledArray<T>>(Environment.ProcessorCount * 16);

        private T[] _array;
        private bool _disposed;
        private int _referenceCount;

        private OwnedPooledArray()
        { }

        [Obsolete("Use TryGetArray")]
        public T[] Array => _array;

        public override int Length => _array.Length;

        public override bool IsDisposed => _disposed;

        public override bool IsRetained => _referenceCount > 0;

        public override Span<T> AsSpan(int index, int length)
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            return new Span<T>(_array, index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedBuffer<T> Create(int size)
        {
            if (!Pool.TryTake(out OwnedPooledArray<T> ownedPooledArray))
            {
                ownedPooledArray = new OwnedPooledArray<T>();
            }
            ownedPooledArray._array = BufferPool<T>.Rent(size, false);
            ownedPooledArray._disposed = false;
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OwnedBuffer<T> Create(T[] array)
        {
            if (!Pool.TryTake(out OwnedPooledArray<T> ownedPooledArray))
            {
                ownedPooledArray = new OwnedPooledArray<T>();
            }
            ownedPooledArray._array = array;
            ownedPooledArray._disposed = false;
            ownedPooledArray._referenceCount = 0;
            return ownedPooledArray;
        }

        protected override void Dispose(bool disposing)
        {
            var array = Interlocked.Exchange(ref _array, null);
            if (array != null)
            {
                _disposed = true;
                BufferPool<T>.Return(array);
            }
            if (disposing)
            {
                Pool.TryAdd(this);
            }
        }

        protected override bool TryGetArray(out ArraySegment<T> buffer)
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            buffer = new ArraySegment<T>(_array);
            return true;
        }

        public override BufferHandle Pin(int index = 0)
        {
            unsafe
            {
                Retain(); // this checks IsDisposed
                var handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
                var pointer = Add((void*)handle.AddrOfPinnedObject(), index);
                return new BufferHandle(this, pointer, handle);
            }
        }

        public override void Retain()
        {
            if (IsDisposed) ThrowHelper.ThrowObjectDisposedException(nameof(OwnedPooledArray<T>));
            Interlocked.Increment(ref _referenceCount);
        }

        public override void Release()
        {
            var newRefCount = Interlocked.Decrement(ref _referenceCount);
            if (newRefCount == 0)
            {
                Dispose();
                return;
            }
            if (newRefCount < 0)
            {
                throw new InvalidOperationException();
            }
        }
    }
}