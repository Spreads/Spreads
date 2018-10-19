// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// Completely not thread-safe with possible segfaults if created/resized from different threads.
    /// </summary>
    public unsafe struct OffHeapBuffer<T> : IDisposable where T : unmanaged
    {
        private static readonly int DefaultMinLength = 16383;

        /// <summary>
        /// Use only after EnsureCapacity call
        /// </summary>
        internal T* PointerUnsafe;

        internal DirectBuffer _db;
        private int _itemLength;

        public OffHeapBuffer(int length)
        {
            _itemLength = default;
            PointerUnsafe = null;
            _db = default;
            EnsureCapacity(length);
        }

        public T* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (PointerUnsafe == null)
                {
                    CheckNullInitDefault();
                }
                return PointerUnsafe;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CheckNullInitDefault()
        {
            EnsureCapacity(DefaultMinLength);
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _itemLength;
        }

        public DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _db;
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<T>(Pointer, Unsafe.SizeOf<T>() * _itemLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(int newLength)
        {
            if (newLength > _itemLength)
            {
                EnsureCapacitySlow(newLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacitySlow(int newLength)
        {
            if (newLength < _itemLength * 2)
            {
                newLength = _itemLength * 2;
            }

            if (PointerUnsafe == null)
            {
                PointerUnsafe = (T*)Marshal.AllocHGlobal(Unsafe.SizeOf<T>() * newLength);
            }
            else
            {
                PointerUnsafe = (T*)Marshal.ReAllocHGlobal((IntPtr)PointerUnsafe,
                    (IntPtr)(Unsafe.SizeOf<T>() * newLength));
            }
            _itemLength = newLength;
            _db = new DirectBuffer(_itemLength, (byte*)PointerUnsafe);
        }

        public void Dispose()
        {
            if (PointerUnsafe != null)
            {
                Marshal.FreeHGlobal((IntPtr)PointerUnsafe);
            }
        }
    }

    internal unsafe struct OffHeapBuffer : IDisposable
    {
        private static readonly int DefaultMinLength = 16383;
        private byte* _bufferPtr;
        private int _length;
        private DirectBuffer _db;

        public OffHeapBuffer(int length)
        {
            _length = default;
            _bufferPtr = null;
            _db = default;
            EnsureCapacity(length);
        }

        public byte* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_bufferPtr == null)
                {
                    CheckNullInitDefault();
                }
                return _bufferPtr;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CheckNullInitDefault()
        {
            EnsureCapacity(DefaultMinLength);
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _db;
        }

        public Span<byte> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<byte>(Pointer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSpan<T>()
        {
            return new Span<T>(Pointer, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int newLength)
        {
            if (newLength > _length)
            {
                EnsureCapacitySlow(newLength);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureCapacitySlow(int newLength)
        {
            if (newLength < _length * 2)
            {
                newLength = _length * 2;
            }

            if (_bufferPtr == null)
            {
                _bufferPtr = (byte*)Marshal.AllocHGlobal(newLength);
            }
            else
            {
                _bufferPtr = (byte*)Marshal.ReAllocHGlobal((IntPtr)_bufferPtr,
                    (IntPtr)(newLength));
            }
            _length = newLength;
            _db = new DirectBuffer(_length, _bufferPtr);
        }

        public void Dispose()
        {
            if (_bufferPtr != null)
            {
                Marshal.FreeHGlobal((IntPtr)_bufferPtr);
            }
        }
    }
}
