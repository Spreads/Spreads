// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
#if DEBUG
#define DETECT_LEAKS
#endif

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.Buffers
{
    // TODO (docs) refine the docs, merge summary and remarks with clearer wording. Remarks > summary.

    /// <summary>
    /// A struct that wraps a <see cref="Memory{T}"/> and its <see cref="MemoryHandle"/> that is returned after calling <see cref="Memory{T}.Pin"/>.
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying MemoryManager implementation
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
    /// needs to retain the memory it must call <see cref="Clone"/> and pass the cloned <see cref="RetainedMemory{T}"/>.
    ///
    /// Access to this struct is not thread-safe, only one thread could call its methods at a time.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct RetainedMemory<T> : IDisposable
    {
        // Could add Deconstruct method
        internal MemoryHandle _memoryHandle;
        internal Memory<T> _memory;

        // Could add up to 24 fields to still fir in one cache line.

        /// <summary>
        /// Create a new RetainedMemory from Memory and pins it.
        /// </summary>
        /// <param name="memory"></param>
        public RetainedMemory(Memory<T> memory)
        {
            _memory = memory;
            _memoryHandle = memory.Pin();
#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        internal RetainedMemory(T[] bytes)
        {
            _memory = (Memory<T>)bytes;
            _memoryHandle = default;
            // We do not need to Pin arrays, they do not have ref count. Will be pinned when Pointer is accessed.
#if DETECT_LEAKS
            _finalizeChecker = new PanicOnFinalize();
#endif
        }

        /// <summary>
        /// Used internally with "fake" handle, that could unpin owned buffer but
        /// does not hold a pinned GCHandle. However, any existing MemoryHandle
        /// should work and the dispose method of RetainedMemory will dispose
        /// MemoryHandle (i.e. ownership of the handle is *transfered* into this ctor).
        /// </summary>
        internal RetainedMemory(Memory<T> memory, MemoryHandle handle)
        {
            _memory = memory;
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
                    return _memoryHandle.Pointer != null;
                }
            }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory.IsEmpty;
        }

        public unsafe void* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var ptr = _memoryHandle.Pointer;
                if (ptr != null) return ptr;
                // replace handles
                var newHandle = Memory.Pin();
                _memoryHandle.Dispose();
                _memoryHandle = newHandle;
                return _memoryHandle.Pointer;
            }
        }

        /// <summary>
        /// Memory.
        /// </summary>
        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory;
        }

        /// <summary>
        /// A shortcut to Memory.Span property.
        /// </summary>
        public Span<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory.Span;
        }

        /// <summary>
        /// Gets the number of elements in the RetainedMemory.
        /// </summary>
        [Obsolete("Use Length property instead")]
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory.Length;
        }

        /// <summary>
        /// Gets the number of elements in the RetainedMemory.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _memory.Length;
        }

        /// <summary>
        /// Slice in-place, keep ownership.
        /// </summary>
        public void Trim(int start, int length)
        {
            _memory = _memory.Slice(start, length);
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
            _memory = default;
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

    public static class RetainedMemoryExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetArray(this RetainedMemory<byte> rm, out ArraySegment<byte> segment)
        {
            return MemoryMarshal.TryGetArray(rm.Memory, out segment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<RetainedMemory<byte>> ToRetainedMemory(this Stream stream, int initialSize = 16 * 1024, int limit = 0)
        {
            RetainedMemory<byte> rm;
            var knownSize = -1;
            if (stream.CanSeek)
            {
                knownSize = checked((int) stream.Length);
                rm = BufferPool.Retain(knownSize);
            }
            else
            {
                rm = BufferPool.Retain(initialSize);
            }
#if NETCOREAPP2_1
            var t = stream.ReadAsync(rm.Memory);
            if (t.IsCompletedSuccessfully && knownSize >= 0 && t.Result == knownSize)
            {
                // Do not need to trim rm, it is exactly of knownSize
                return new ValueTask<RetainedMemory<byte>>(rm);
            }
            return ToRetainedMemoryAsync(t);
#else
            return ToRetainedMemoryAsync(default);
#endif

            async ValueTask<RetainedMemory<byte>> ToRetainedMemoryAsync(ValueTask<int> started)
            {
                var memory = rm.Memory;
                var totalRead = 0;
                int read;
                if (started != default)
                {
                    read = await started;
                    if (knownSize >= 0 && read == knownSize)
                    {
                        // Do not need to trim rm, it is exactly of knownSize
                        return rm;
                    }

                    totalRead += read;
                    memory = memory.Slice(read);
                    if (totalRead == rm.Length)
                    {
                        var rm2 = BufferPool.Retain(rm.Length * 2);
                        rm.Memory.CopyTo(rm2.Memory);
                        memory = rm2.Memory.Slice(totalRead);
                        rm = rm2;
                    }
                }
#if NETCOREAPP2_1
                while ((read = await stream.ReadAsync(memory)) > 0)
                {
#else
                var buffer = BufferPool<byte>.Rent(initialSize);
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ((Memory<byte>)buffer).Slice(0, read).CopyTo(memory);
#endif
                    totalRead += read;

                    if (limit > 0 & totalRead > limit)
                    {
                        ThrowHelper.ThrowInvalidOperationException($"Reached Stream.ToRetainedMemory limit {limit}.");
                    }

                    memory = memory.Slice(read);
                    if (totalRead == rm.Length)
                    {
                        var rm2 = BufferPool.Retain(rm.Length * 2);
                        rm.Memory.CopyTo(rm2.Memory);
                        memory = rm2.Memory.Slice(totalRead);
                        rm = rm2;
                    }
                }
#if !NETCOREAPP2_1
                BufferPool<byte>.Return(buffer);
#endif
                rm.Trim(0, totalRead);
                return rm;

            }
        }

    }
}
