// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using SRCS = System.Runtime.CompilerServices;

namespace Spreads.Buffers
{

    public sealed class ArrayMemoryPool<T> : MemoryPool<T>
    {
        public new static ArrayMemoryPool<T> Shared = new ArrayMemoryPool<T>();

        private ArrayMemoryPool()
        { }

        // ReSharper disable once InconsistentNaming
        private const int s_maxBufferSize = int.MaxValue;

        public override int MaxBufferSize => s_maxBufferSize;

        internal ArrayMemoryPoolBuffer<T> RentCore(int minimumBufferSize = -1)
        {
            if (minimumBufferSize == -1)
            {
                minimumBufferSize = 1 + (4095 / Unsafe.SizeOf<T>());
            }
            else if (((uint)minimumBufferSize) > s_maxBufferSize)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(minimumBufferSize));
            }

            return new ArrayMemoryPoolBuffer<T>(minimumBufferSize);
        }

        public override IMemoryOwner<T> Rent(int minimumBufferSize = -1)
        {
            return RentCore(minimumBufferSize);
        }

        protected override void Dispose(bool disposing)
        {
        }  // ArrayMemoryPool is a shared pool so Dispose() would be a nop even if there were native resources to dispose.
    }

    internal sealed class ArrayMemoryPoolBuffer<T> : MemoryManager<T>
    {
        // TODO Object pool!

        private T[] _array;
        private int _refCount;

        // TODO Object pooling
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayMemoryPoolBuffer(int size)
        {
            _array = ArrayPool<T>.Shared.Rent(size);
            _refCount = 1;
        }

        public bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _array == null; }
        }

        public bool IsRetained
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Volatile.Read(ref _refCount) > 0; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override Span<T> GetSpan()
        {
            if (_array == null)
            {
                ThrowHelper.ThrowObjectDisposedException("ArrayMemoryPoolBuffer");
            }

            return _array;
        }

        protected  override void Dispose(bool disposing)
        {
            if (_array != null)
            {
                ArrayPool<T>.Shared.Return(_array);
                _array = null;
            }
        }

        public new bool TryGetArray(out ArraySegment<T> segment)
        {
            if (IsDisposed)
            {
                ThrowHelper.ThrowObjectDisposedException("ArrayMemoryPoolBuffer");
            }
            segment = new ArraySegment<T>(_array);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            unsafe
            {
                while (true)
                {
                    int currentCount = Volatile.Read(ref _refCount);

                    if (currentCount <= 0)
                    {
                        ThrowHelper.ThrowObjectDisposedException("ArrayMemoryPoolBuffer");
                    }

                    if (Interlocked.CompareExchange(ref _refCount, currentCount + 1, currentCount) == currentCount)
                    {
                        break;
                    }
                }

                try
                {
                    if ((uint)elementIndex > (uint)_array.Length)
                    {
                        ThrowHelper.ThrowArgumentOutOfRangeException("elementIndex");
                    }

                    GCHandle handle = GCHandle.Alloc(_array, GCHandleType.Pinned);

                    return new MemoryHandle(SRCS.Unsafe.Add<T>(((void*)handle.AddrOfPinnedObject()), elementIndex), handle, this);
                }
                catch
                {
                    Unpin();
                    throw;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Unpin()
        {
            while (true)
            {
                int currentCount = Volatile.Read(ref _refCount);
                if (currentCount <= 0)
                {
                    ThrowHelper.ThrowObjectDisposedException("ArrayMemoryPoolBuffer");
                }

                if (Interlocked.CompareExchange(ref _refCount, currentCount - 1, currentCount) == currentCount)
                {
                    if (currentCount == 1)
                    {
                        Dispose(true);
                    }
                    break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            Dispose(true);
        }

        public override Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                T[] array = _array;
                if (array == null)
                {
                    ThrowHelper.ThrowObjectDisposedException("ArrayMemoryPoolBuffer");
                }

                return new Memory<T>(array);
            }
        }
    }
}