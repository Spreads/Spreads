// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Spreads.Utils;

namespace Spreads.Buffers
{
    /// <summary>
    /// Configurable native memory allocate/free delegates. Default implementations uses
    /// <see cref="Marshal.AllocHGlobal(int)"/> and <see cref="Marshal.FreeHGlobal"/>.
    /// </summary>
    /// <remarks>
    /// Native memory is pooled and performance of a native allocator is not a big concern.
    /// Rather memory usage and reclamation behavior are much more important. Some allocators
    /// keep too much memory per thread, not releasing it back to OS proactively.
    /// See e.g. https://github.com/microsoft/snmalloc/issues/127 and related commit and Twitter discussion.
    /// </remarks>
    public unsafe class NativeAllocator
    {
        private static readonly ILogger _log = Logger.ForType<NativeAllocator>();
        
        /// <summary>
        /// Allocated non-initialized native memory. Some allocators, such as Mimalloc or Jemalloc,
        /// could allocate more usable memory than requested. Use <paramref name="usableSize"/>
        /// value for that. An implementation must throw <see cref="OutOfMemoryException"/> if an
        /// allocation fails (not just return null pointer).
        /// </summary>
        /// <param name="requiredSize">Minimum size of allocated memory.</param>
        /// <param name="usableSize">Actual usable size of allocated memory.
        /// Must be equal to or greater than <paramref name="requiredSize"/> (not zero or negative
        /// if extended allocation is not supported).
        /// </param>
        /// <exception cref="OutOfMemoryException"></exception>
        public delegate byte* AllocateDelegate(nuint requiredSize, out nuint usableSize);

        /// <summary>
        /// Free memory allocated by <see cref="Allocate"/>
        /// </summary>
        /// <param name="memory">A pointer to native memory returned by <see cref="NativeAllocator.Allocate"/></param>
        public delegate void FreeDelegate(byte* memory);

        // TODO (low) CleanUp delegate for ThreadPool extended spinning

        public static AllocateDelegate Allocate { get; private set; } = DefaultAllocate();

        private static AllocateDelegate DefaultAllocate()
        {
            return (nuint requiredSize, out nuint usableSize) =>
            {
#if NET6_0
                var ptr = (byte*)NativeMemory.Alloc(requiredSize);
#else
                var ptr = (byte*)Marshal.AllocHGlobal((nint)requiredSize);
#endif
                usableSize = requiredSize;
                return ptr;
            };
        }

        public static FreeDelegate Free { get; private set; } = DefaultFree();

        private static FreeDelegate DefaultFree()
        {
            return memory =>
            {
#if NET6_0
                NativeMemory.Free(memory);
#else
                Marshal.FreeHGlobal((IntPtr)memory);
#endif
            };
        }

        /// <summary>
        /// Delegates must be set before any usage of <see cref="PrivateMemory{T}"/> and other
        /// types that may use native memory, otherwise bad things could happen
        /// (much worse than memory leaks - segfault, undefined behavior, etc).
        /// </summary>
        /// <param name="allocateDelegate"></param>
        /// <param name="freeDelegate"></param>
        public static void SetDelegates(AllocateDelegate allocateDelegate, FreeDelegate freeDelegate)
        {
            _log.LogInformation("Setting native allocators.");

            // detect wrong implementation early, log before potential unmanaged fail.
            for (int i = 1000; i <= 10000; i += 100)
            {
                freeDelegate(allocateDelegate((nuint)i, out _));
            }

            Allocate = allocateDelegate;
            Free = freeDelegate;
        }
    }
}
