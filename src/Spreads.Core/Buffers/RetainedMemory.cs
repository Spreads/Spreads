// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers
{
    // TODO (docs) refine the docs, merge summary and remarks with clearer wording. Remarks > summary.

    /// <summary>
    /// A struct that wraps a <see cref="Memory{T}"/> and its <see cref="MemoryHandle"/> that is returned after calling <see cref="Memory{T}.Pin"/>.
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying OwnedPooledArray
    /// will never be returned to the pool and memory will leak.
    /// Use <see cref="Clone"/> method to create a copy of this memory and to ensure that the underlying <see cref="Buffers.OwnedPooledArray{T}"/> is not returned to the pool.
    /// When adding to a Spreads disposable collection (e.g. SortedMap) ownership is transfered to a collection and RetainedMemory
    /// will be disposed during disposal of that collection. To keep ownership outside the collection, use the <see cref="Clone"/> method and
    /// add a cloned RetainedMemory value to the collection.
    /// </summary>
    /// <remarks>
    /// <see cref="RetainedMemory{T}"/> is the owner of <see cref="MemoryHandle"/> reservation.
    /// When it is passed to any method or added  to any collection the reservation ownership is transfered as well.
    /// The consuming method or collection must dispose the <see cref="MemoryHandle"/> reservation. If the caller
    /// needs to retain the memory and must call <see cref="Clone"/> and pass the cloned memory.
    /// </remarks>
    public struct RetainedMemory<T> : IDisposable
    {
        private MemoryHandle _memoryHandle;

        /// <summary>
        /// Create a new RetainedMemory from Memory and pins it.
        /// </summary>
        /// <param name="memory"></param>
        public RetainedMemory(Memory<T> memory)
        {
            Memory = memory;
            _memoryHandle = memory.Pin();
#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        internal RetainedMemory(Memory<T> memory, MemoryHandle handle)
        {
            Memory = memory;
            _memoryHandle = handle;
#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                unsafe
                {
                    return _memoryHandle.Pointer != (void*)IntPtr.Zero;
                }
            }
        }

        public unsafe void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsPinned) return _memoryHandle.Pointer;
                // replace handles
                var newHandle = Memory.Pin();
                _memoryHandle.Dispose();
                _memoryHandle = newHandle;
                return _memoryHandle.Pointer;
            }
        }

        /// <summary>
        /// Memory
        /// </summary>
        public Memory<T> Memory { get; private set; }

        /// <summary>
        /// A shortcut to Memory.Span property.
        /// </summary>
        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Memory.Span; }
        }

        /// <summary>
        /// Gets the number of elements in the RetainedMemory.
        /// </summary>
        [Obsolete("Use Length property instead")]
        public int Count => Memory.Length;

        /// <summary>
        /// Gets the number of elements in the RetainedMemory.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Memory.Length; }
        }

        /// <summary>
        /// Gets the element at the specified index in the RetainedMemory.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Memory.Span[index]; }
        }

        /// <summary>
        /// Release a reference of the underlying OwnedBuffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if DETECT_LEAKS
            _finalizeChecker.Dispose();
#endif
            _memoryHandle.Dispose();
            Memory = default;
        }

        /// <summary>
        /// Increment the underlying OwnedBuffer reference count and return a copy of this preserved memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone()
        {
            return new RetainedMemory<T>(Memory);
        }

#if DETECT_LEAKS
        internal class PanicOnFinalize : IDisposable
        {
            public bool Disposed;
            public string Callstack = System.Environment.StackTrace;

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
}
