// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
#if DEBUG
#define DETECT_LEAKS
#endif

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory(Memory<T> memory)
        {
            if (MemoryMarshal.TryGetMemoryManager<T, RetainableMemory<T>>(memory, out var manager))
            {
                manager.Increment();
                _manager = manager;
                _offset = 0;
                _length = memory.Length;
            }
            else if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
            {
                _manager = ArrayMemory<T>.Create(segment.Array, segment.Offset, segment.Count, true);
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

        [Obsolete("Always pinned")]
        public bool IsPinned
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
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
            get => Unsafe.Add<T>(_manager._pointer, _offset);
        }

        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _manager.Memory.Slice(_offset, _length);
        }

        public unsafe Span<T> Span
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Slice(int start)
        {
            return Slice(start, _length - start);
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone()
        {
#if DETECT_LEAKS
            return new RetainedMemory<T>(_manager, _offset, _length, true, null);
#else
            return new RetainedMemory<T>(_manager, _offset, _length, true);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RetainedMemory<T> Clone(int start)
        {
            return Clone(start, _length - start);
        }

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

        [Obsolete("Do not use streams if possible")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<RetainedMemory<byte>> ToRetainedMemory(this Stream stream, int initialSize = 16 * 1024, int limit = 0)
        {
            // We are capuring rm in async and cannot reason what is going on.
            // Need to folow the RM RULE and pass ownership to async completely
            throw new Exception("Dropping RM ref and async impl is messy");
            RetainedMemory<byte> rm;
            var knownSize = -1;
            if (stream.CanSeek)
            {
                knownSize = checked((int)stream.Length);
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
                        rm.Dispose();
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
                        rm.Dispose();
                        rm = rm2;
                    }
                }
#if !NETCOREAPP2_1
                BufferPool<byte>.Return(buffer);
#endif

                return rm.Slice(0, totalRead);
            }
        }
    }
}
