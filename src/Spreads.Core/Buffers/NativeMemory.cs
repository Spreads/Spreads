using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// using System.Security;
using Spreads.Utils;

namespace Spreads.Buffers
{
    /// <summary>
    /// A helper struct to encapsulate aligned alloc/free of native memory.
    /// </summary>
    /// <remarks>
    /// The intended usage is as a temporary holder of values (like a tuple),
    /// not to be stored as a field. <see cref="PrivateMemory{T}"/> destructures
    /// this into it's fields and then reassembles for freeing. 
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    internal readonly unsafe struct NativeMemory
    {
        public readonly byte* Pointer;

        public readonly uint Length;

        /// <summary>
        /// Offset from <see cref="Pointer"/> to where <see cref="Length"/> starts.
        /// </summary>
        public readonly byte AlignmentOffset;

        // 3 bytes for any flags if ever needed.

        // public byte* Pointer => _pointer + _alignmentOffset;

        public NativeMemory(byte* pointer, byte alignmentOffset, uint length)
        {
            Pointer = pointer;
            AlignmentOffset = alignmentOffset;
            Length = length;
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public static NativeMemory Alloc(uint bytesLength, uint alignment = 8)
        {
            Debug.Assert(BitUtil.IsPowerOfTwo((int)alignment), "BitUtil.IsPowerOfTwo(alignment)");
            
            // It doesn't make a lot of sense to have it above a cache line (or 64 bytes for AVX512).
            // But cache line could be 128 (already exists), CUDA could have 256 bytes alignment,
            // and we could store offset in a single byte for up to 256 bytes alignment.
            alignment = Math.Min(Math.Max(8, alignment), Settings.SAFE_CACHE_LINE * 2);

            // if (BitUtil.IsAligned(bytesLength, Settings.PAGE_SIZE))
            // {
            //     if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //     {
            //         ptr = Windows.VirtualAlloc(lpAddress: null, bytesLength, Windows.MEM_COMMIT | Windows.MEM_RESERVE,
            //             Windows.PAGE_READWRITE);
            //
            //         if (ptr == null)
            //             throw new OutOfMemoryException();
            //
            //         return new NativeMemory(ptr, 0, bytesLength);
            //     }
            // }

            var ptr = Native.Mem.MallocAligned((UIntPtr) bytesLength, (UIntPtr) alignment);
            return new NativeMemory(ptr, 0, bytesLength);

            // // AllocHGlobal already aligns to IntPtr.Size
            // var allocSize = bytesLength + alignment - IntPtr.Size;
            // var ptr = (byte*) Marshal.AllocHGlobal((IntPtr) allocSize);
            // var start = (byte*) BitUtil.Align((long) ptr, alignment);
            // var offset = checked((byte) ((long) start - (long) ptr));
            // return new NativeMemory(start, offset, bytesLength);
        }


        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        public void Free()
        {
            Native.Mem.Free(Pointer);
            // if (BitUtil.IsAligned(Length, Settings.PAGE_SIZE))
            // {
            //     ThrowHelper.DebugAssert(AlignmentOffset == 0);
            //     if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //     {
            //         Windows.VirtualFree(Pointer, 0, Windows.MEM_RELEASE);
            //         return;
            //     }
            // }
            //
            // Marshal.FreeHGlobal((IntPtr) Pointer);
        }

        // private static class Windows
        // {
        //     public const int MEM_COMMIT = 0x00001000;
        //     public const int MEM_RESERVE = 0x00002000;
        //     public const int MEM_RELEASE = 0x8000;
        //     public const int PAGE_READWRITE = 0x04;
        //
        //     [DllImport("Kernel32", SetLastError = true)]
        //     [SuppressUnmanagedCodeSecurity]
        //     public static extern byte* VirtualAlloc([In] byte* lpAddress, [In] uint dwSize, [In] int flAllocationType,
        //         [In] int flProtect);
        //
        //     [DllImport("Kernel32", SetLastError = true)]
        //     [SuppressUnmanagedCodeSecurity]
        //     public static extern bool VirtualFree([In] byte* lpAddress, [In] uint dwSize, [In] int dwFreeType);
        // }
    }
}