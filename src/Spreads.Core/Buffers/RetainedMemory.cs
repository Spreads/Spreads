// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

#if DEBUG
#define DETECT_LEAKS
#endif

using Spreads.Native;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Collections;

namespace Spreads.Buffers
{
    /// <summary>
    /// A borrowing of <see cref="RetainableMemory{T}"/> that owns a reference from it.
    /// </summary>
    /// <remarks>
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying MemoryManager implementation
    /// will never be returned to the pool and memory will leak or frequent GC will occur.
    /// <para/>
    /// RULE: Ownership of <see cref="RetainedMemory{T}"/> is transferred in any method call without ref modifier (in modifier transfers ownership).
    /// Use <see cref="Clone()"/> method or its <see cref="Span{T}.Slice(int,int)"/>-like overloads to create a new borrowing of the underlying memory and to
    /// ensure that the underlying <see cref="RetainableMemory{T}"/> is not returned to the pool.
    /// <para/>
    /// Access to this struct is not thread-safe, only one thread could call its methods at a time.
    /// </remarks>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct RetainedMemory<T> : IDisposable
    {
        internal readonly RetainableMemory<T> _manager;
        internal readonly int _start;
        private readonly int _length;

        /// <summary>
        /// Create a new RetainedMemory from Memory and pins it.
        /// </summary>
        /// <param name="memory"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RetainedMemory(Memory<T> memory)
        {
            if (MemoryMarshal.TryGetMemoryManager<T, RetainableMemory<T>>(memory, out var manager))
            {
                manager.Increment();
                _manager = manager;
                _start = 0;
                _length = memory.Length;
            }
            else if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
            {
                _manager = ArrayMemory<T>.Create(segment.Array, segment.Offset, segment.Count, externallyOwned: true);
                _manager.Increment();
                _start = 0;
                _length = _manager.Length;
            }
            else
            {
                ThrowNotSupportedMemoryType();
                _manager = default;
                _start = 0;
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
        internal RetainedMemory(RetainableMemory<T> memory, int start, int length, bool borrow)
        {
            if (memory.IsDisposed)
                BuffersThrowHelper.ThrowDisposed<RetainableMemory<T>>();

            Debug.Assert(unchecked((uint) start + (uint) length <= memory.Length));

            if (borrow)
                memory.Increment();

            _manager = memory;
            _start = start;
            _length = length;

#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        public unsafe bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.Pointer != null;
        }

        [Obsolete("Prefer fixed statements on a pinnable reference for short-lived pinning")]
        public MemoryHandle Pin(int elementIndex = 0)
        {
            return _manager.Pin(elementIndex);
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        public unsafe void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsPinned ? Unsafe.Add<T>(_manager.Pointer, _start) : (void*) IntPtr.Zero;
        }

        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.Memory.Slice(_start, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSpan()
        {
            return _manager.GetSpan().Slice(_start, _length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec<T> GetVec()
        {
            return _manager.GetVec().Slice(_start, _length);
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
        /// Create a copy and increment the reference counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone()
        {
            return DangerousClone(start: 0, _length);
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
            if (unchecked((uint) start + (uint) length <= _length))
            {
                return DangerousClone(start, length);
            }

            BuffersThrowHelper.ThrowBadLength();
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RetainedMemory<T> DangerousClone(int start, int length)
        {
            if (!_manager.IsRetained)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot clone not retained memory.");
            }

            return new RetainedMemory<T>(_manager, _start + start, length, borrow: true);
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
            if (_manager?.ReferenceCount == 0)
            {
                // This is "retained" memory, but ref count is zero,
                // which is only possible if it was created with borrow = false
                // in the internal ctor. Note that it's not possible that
                // someone Clones/Retains the underlying RM because it's
                // only accessible from this struct and Clone throws when not retained.
                // Also RC 1 -> 0 disposes RM. This is used internally for
                // getting temporary buffers as RetainedMemory and avoid
                // interlocked
                _manager.Dispose();
            }
            else
            {
                _manager?.Decrement();
            }
        }

        /// <summary>
        /// Stop tracking finalization.
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
            public string CallStack = Environment.StackTrace;

            ~PanicOnFinalize()
            {
                if (Disposed)
                {
                    // sanity check
                    ThrowHelper.ThrowInvalidOperationException(
                        $"Finalizer was called despite being disposed: {CallStack}");
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperationException(
                        $"RetainedMemory was not properly disposed and is being finalized: {CallStack}");
                }
            }

            public void Dispose()
            {
                if (Disposed)
                {
                    ThrowHelper.ThrowInvalidOperationException(
                        $"Retained memory was already disposed. Check your code that passes it by value without calling .Clone(): {CallStack}");
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
            // throw new NotImplementedException("What about R-edM start & length!?"); // TODO fix & test
            if (MemoryMarshal.TryGetArray(rm.Memory, out segment))
            {
                segment = new ArraySegment<byte>(segment.Array!, segment.Offset + rm._start, rm.Length);
                return true;
            }

            return false;
        }

        /// <summary>
        /// A shortcut to <see cref="DirectBuffer"/> ctor that accepts <see cref="RetainedMemory{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DirectBuffer ToDirectBuffer(this RetainedMemory<byte> rm)
        {
            if (!rm.IsPinned)
            {
                ThrowHelper.ThrowInvalidOperationException("Cannot create DirectBuffer from RetainedMemory because it is not pinned.");
            }

            return new DirectBuffer(rm);
        }
    }
}