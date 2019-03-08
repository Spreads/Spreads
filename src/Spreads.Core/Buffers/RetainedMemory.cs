// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
#if DEBUG
#define DETECT_LEAKS
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    /// <summary>
    /// A borrowing of <see cref="RetainableMemory{T}"/> that owns a reference from it.
    /// </summary>
    /// <remarks>
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying MemoryManager implementation
    /// will never be returned to the pool and memory will leak.
    /// RULE: Ownership of <see cref="RetainedMemory{T}"/> is transfered in any method call without ref modifier (in modifier transfers ownership).
    /// Use <see cref="Clone()"/> method or its Slice-like overloads to create a copy of this memory and to
    /// ensure that the underlying <see cref="RetainableMemory{T}"/> is not returned to the pool.
    /// Access to this struct is not thread-safe, only one thread could call its methods at a time.
    /// </remarks>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct RetainedMemory<T> : IDisposable
    {
        internal readonly RetainableMemory<T> _manager;
        private readonly int _offset;
        private readonly int _length;

        /// <summary>
        /// Create a new RetainedMemory from Memory and pins it.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="pin"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory(Memory<T> memory, bool pin = true) // TODO pin param added later and before it behaved like with true, but better to change to false and review usage
        {
            if (MemoryMarshal.TryGetMemoryManager<T, RetainableMemory<T>>(memory, out var manager))
            {
                if (!manager.IsPinned && pin)
                {
                    // TODO review. This uses implementation detail of RetainableMemory:
                    // if pointer is null then it is an non-pinned array for which we did not create
                    // a GCHandle (very expensive). Call to Pin() checks if pointer is null and 
                    // creates a GCHandle + pointer. Try to avoid pinning non-pooled ArrayMemory
                    // because it is very expensive.
                    manager.Pin();
                }
                else
                {
                    manager.Increment();
                }
                _manager = manager;
                _offset = 0;
                _length = memory.Length;
            }
            else if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
            {
                _manager = ArrayMemory<T>.Create(segment.Array, segment.Offset, segment.Count, externallyOwned: true, pin);
                _manager.Increment();
                _offset = 0;
                _length = _manager.Length;
            }
            else
            {
                ThrowNotSupportedMemoryType();
                _manager = default;
                _offset = 0;
                _length = 0;
            }

#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotSupportedMemoryType()
        {
            ThrowHelper.ThrowNotSupportedException("Only RetainableMemory<T> and array-backed Memory is supported");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if DETECT_LEAKS
        internal RetainedMemory(RetainableMemory<T> memory, int offset, int length, bool borrow, PanicOnFinalize checker = null)
#else
        internal RetainedMemory(RetainableMemory<T> memory, int offset, int length, bool borrow)
#endif
        {
            if (memory.IsDisposed)
            {
                BuffersThrowHelper.ThrowDisposed<RetainableMemory<T>>();
            }
            Debug.Assert(unchecked((uint)offset + (uint)length <= memory.Length));

            if (borrow)
            {
                memory.Increment();
            }

            _manager = memory;
            _offset = offset;
            _length = length;

            // We do not need to Pin arrays, they do not have ref count. Will be pinned when Pointer is accessed.
#if DETECT_LEAKS
            _finalizeChecker = borrow ? new PanicOnFinalize() : checker;
#endif
        }

        public unsafe bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.Pointer != null;
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager != null;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        public unsafe void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.Add<T>(_manager.Pointer, _offset);
        }

        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.Memory.Slice(_offset, _length);
        }

        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.GetSpan().Slice(_offset, _length); // new Span<T>(Pointer, _length);
        }

        /// <summary>
        /// Gets the number of elements in the RetainedMemory.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Slice without incrementing the reference counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Slice(int start)
        {
            return Slice(start, _length - start);
        }

        /// <summary>
        /// Slice without incrementing the reference counter.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Slice(int start, int length)
        {
            if (unchecked((uint)start + (uint)length <= _length))
            {
#if DETECT_LEAKS
                return new RetainedMemory<T>(_manager, _offset + start, length, false, _finalizeChecker);
#else
                return new RetainedMemory<T>(_manager, _offset + start, length, false);
#endif
            }
            BuffersThrowHelper.ThrowBadLength();
            return default;
        }

        /// <summary>
        /// Create a copy and increment the reference counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone()
        {
#if DETECT_LEAKS
            return new RetainedMemory<T>(_manager, _offset, _length, true, null);
#else
            return new RetainedMemory<T>(_manager, _offset, _length, true);
#endif
        }

        /// <summary>
        /// Create a sliced copy and increment the reference counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone(int start)
        {
            return Clone(start, _length - start);
        }

        /// <summary>
        /// Create a sliced copy and increment the reference counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone(int start, int length)
        {
            if (unchecked((uint)start + (uint)length <= _length))
            {
#if DETECT_LEAKS
                return new RetainedMemory<T>(_manager, _offset + start, length, true, null);
#else
                return new RetainedMemory<T>(_manager, _offset + start, length, true);
#endif
            }
            BuffersThrowHelper.ThrowBadLength();
            return default;
        }

        public int ReferenceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.ReferenceCount;
        }

        public override string ToString()
        {
            return $"RM: Length={Length}, RefCount={_manager?.ReferenceCount}";
        }

        /// <summary>
        /// Release a reference of the underlying OwnedBuffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if DETECT_LEAKS
            _finalizeChecker?.Dispose();
#endif
            _manager?.Decrement();
        }

        /// <summary>
        /// Release a reference of the underlying OwnedBuffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Forget()
        {
#if DETECT_LEAKS
            _finalizeChecker?.Dispose();
#endif
        }

#if DETECT_LEAKS

        internal class PanicOnFinalize : IDisposable
        {
            public bool Disposed;
            public string Callstack = Environment.StackTrace;

            ~PanicOnFinalize()
            {
                if (Disposed)
                {
                    // sanity check
                    ThrowHelper.ThrowInvalidOperationException(
                        $"Finalizer was called despite being disposed: {Callstack}");
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException(
                        $"Retained memory was not properly disposed and is being finalized: {Callstack}");
                }
            }

            public void Dispose()
            {
                if (Disposed)
                {
                    ThrowHelper.ThrowInvalidOperationException(
                        $"Retained memory was already disposed. Check your code that passes it by value without calling .Clone(): {Callstack}");
                }
                GC.SuppressFinalize(this);
                Disposed = true;
            }
        }

        internal readonly PanicOnFinalize _finalizeChecker;
#endif
    }

    public static class RetainedMemoryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetArray(this RetainedMemory<byte> rm, out ArraySegment<byte> segment)
        {
            return MemoryMarshal.TryGetArray(rm.Memory, out segment);
        }

        /// <summary>
        /// A shortcut to <see cref="DirectBuffer"/> ctor that accepts <see cref="RetainedMemory{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectBuffer ToDirectBuffer(this RetainedMemory<byte> rm)
        {
            return new DirectBuffer(rm);
        }
    }
}
