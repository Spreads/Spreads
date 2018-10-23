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
    public unsafe struct OffHeapBuffer<T> : IPinnedSpan<T> //where T : struct
    {
        private static readonly int DefaultMinLength = 16383;

        /// <summary>
        /// Use only after EnsureCapacity call
        /// </summary>
        internal void* _pointer;

        // internal DirectBuffer _db;
        private int _itemLength;

        // cache it because it accessed a lot and OffHeapBuffers are intended
        // to be alive for a long time and pooled so there are not too many of them
        private DirectBuffer _directBuffer;

        public OffHeapBuffer(int length)
        {
            _itemLength = default;
            _pointer = null;
            _directBuffer = default;
            // ReSharper disable once ExpressionIsAlwaysNull
            EnsureCapacitySlow(length);
        }

        public void* Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_pointer == null)
                {
                    CheckNullInitDefault();
                }
                return _pointer;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CheckNullInitDefault()
        {
            EnsureCapacity(DefaultMinLength);
        }

        /// <inheritdoc />
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _itemLength;
        }

        /// <inheritdoc />
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _itemLength == 0;
        }

        /// <inheritdoc />
        public DirectBuffer DirectBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _directBuffer;
        }

        /// <inheritdoc />
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef<T>(Unsafe.Add<T>(Data, index));
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<T>(Data, _itemLength);
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

            if (_pointer == null)
            {
                _pointer = (void*)Marshal.AllocHGlobal(Unsafe.SizeOf<T>() * newLength);
            }
            else
            {
                _pointer = (void*)Marshal.ReAllocHGlobal((IntPtr)_pointer,
                    (IntPtr)(Unsafe.SizeOf<T>() * newLength));
            }
            _itemLength = newLength;

            _directBuffer = new DirectBuffer(_itemLength * Unsafe.SizeOf<T>(), (byte*)_pointer);
        }

        public void Dispose()
        {
            if (_pointer != null)
            {
                Marshal.FreeHGlobal((IntPtr)_pointer);
            }
        }
    }
}
