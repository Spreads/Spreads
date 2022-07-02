using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Utils;
using System.Collections.Generic;
using System.Security;

namespace Spreads.Core.Tests.Performance
{
    [TestFixture]
    public class GCTests
    {
#if NETCOREAPP
        [Test, Explicit]
        public unsafe void GetGcMemoryInfo()
        {
            // var len = 10L * 1024 * 1024 * 1024;
            // var ptr = Marshal.AllocHGlobal((IntPtr)len);
            // for (long i = 0; i < len; i++)
            // {
            //     *((byte*) ptr + i) = (byte)(i & 255);
            // }

            var stat = GC.GetGCMemoryInfo();
            Console.WriteLine($"HeapSizeBytes: {stat.HeapSizeBytes:N0}");
            Console.WriteLine($"MemoryLoadBytes: {stat.MemoryLoadBytes:N0}");
            Console.WriteLine($"TotalAvailableMemoryBytes: {stat.TotalAvailableMemoryBytes:N0}");
            Console.WriteLine($"FragmentedBytes: {stat.FragmentedBytes:N0}");
            Console.WriteLine($"HighMemoryLoadThresholdBytes: {stat.HighMemoryLoadThresholdBytes:N0}");
        }
#endif
        private const int alloc_free_iterations = 100_000;

        // [Test, Explicit]
        // public unsafe void MimallocAllocFreePerf()
        // {
        //     var count = TestUtils.GetBenchCount(alloc_free_iterations);
        //     var size = 128 * 1024;
        //     IntPtr[] ptrs = new IntPtr[count];
        //
        //     for (int r = 0; r < 3; r++)
        //     {
        //
        //         using (Benchmark.Run("Alloc" + r, count * size))
        //         {
        //             for (int i = 0; i < count; i++)
        //             {
        //                 ptrs[i] = (IntPtr)Spreads.Native.Mem.Malloc((UIntPtr)size);
        //                 // if (!BitUtil.IsAligned((long) (ptrs[i]), 4096))
        //                 // {
        //                 //     Console.WriteLine((long) (ptrs[i]) % 4096);
        //                 //
        //                 // }
        //
        //                 for (int j = 0; j < size; j += 4096) ((byte*)ptrs[i])[j] = 0;
        //                 ((byte*)ptrs[i])[size - 1] = 0;
        //             }
        //         }
        //
        //         using (Benchmark.Run("Free" + r, count * size))
        //         {
        //             for (int i = 0; i < count; i++)
        //             {
        //                 Spreads.Native.Mem.Free((byte*)ptrs[i]);
        //             }
        //         }
        //     }
        //
        //     // long x = 0;
        //     // while (true)
        //     // {
        //     //     x++;
        //     //
        //     //     ptrs[0] = (IntPtr) Spreads.Native.Mem.Malloc((uint) size);
        //     //     Spreads.Native.Mem.Free((byte*) ptrs[0]);
        //     //     if(x == long.MaxValue)
        //     //         break;
        //     //
        //     //
        //     // }
        //     // Spreads.Native.Mem.Collect(false);
        //     // Thread.Sleep(10000000);
        // }

        [Test, Explicit]
        public unsafe void MarshalAllocFreePerf()
        {
            var count = TestUtils.GetBenchCount(alloc_free_iterations);
            var size = 128 * 1024;
            IntPtr[] ptrs = new IntPtr[count];
            using (Benchmark.Run("Alloc", count * size))
            {
                for (int i = 0; i < count; i++)
                {
                    ptrs[i] = Marshal.AllocHGlobal(size);
                    // if (!BitUtil.IsAligned((long) (ptrs[i]), 4096))
                    // {
                    //     Console.WriteLine((long) (ptrs[i]) % 4096);
                    //
                    // }

                    for (int j = 0; j < size; j += 4096) ((byte*)ptrs[i])[j] = 0;
                    ((byte*)ptrs[i])[size - 1] = 0;
                }
            }

            using (Benchmark.Run("Free", count * size))
            {
                for (int i = 0; i < count; i++)
                {
                    Marshal.FreeHGlobal(ptrs[i]);
                }
            }
        }

        [Test, Explicit]
        public unsafe void VirtualAllocFreePerf()
        {
            var count = TestUtils.GetBenchCount(alloc_free_iterations);
            var size = 128 * 1024;
            IntPtr[] ptrs = new IntPtr[count];
            using (Benchmark.Run("Alloc", count * size))
            {
                for (int i = 0; i < count; i++)
                {
                    ptrs[i] = Kernel32.VirtualAlloc(IntPtr.Zero, (uint)size, Kernel32.Consts.MEM_COMMIT | Kernel32.Consts.MEM_RESERVE, Kernel32.Consts.PAGE_READWRITE);
                    if (!BitUtils.IsAligned((long)(ptrs[i]), 4096))
                    {
                        Console.WriteLine((long)(ptrs[i]) % 4096);
                    }

                    for (int j = 0; j < size; j += 4096) ((byte*)ptrs[i])[j] = 0;
                    ((byte*)ptrs[i])[size - 1] = 0;
                }
            }

            using (Benchmark.Run("Free", count * size))
            {
                for (int i = 0; i < count; i++)
                {
                    Kernel32.VirtualFree(ptrs[i], 0, Kernel32.Consts.MEM_RELEASE);
                }
            }
        }

        [Test, Explicit]
        public unsafe void GcAllocFreePerf()
        {
            var count = (int)TestUtils.GetBenchCount(alloc_free_iterations);
            var size = 128 * 1024;

            List<byte[]> arrays = new List<byte[]>(count);
            using (Benchmark.Run("Alloc", count * size))
            {
                for (int i = 0; i < count; i++)
                {
                    var arr = new byte[size];
                    for (int j = 0; j < size; j += 4096) arr[j] = 0;
                    arr[size - 1] = 0;
                    arrays.Add(arr);
                }
            }

            using (Benchmark.Run("Free", count * size))
            {
                arrays = null;
                GC.Collect(2, GCCollectionMode.Forced, true);
            }
        }
    }

    public static class Kernel32
    {
        [DllImport("Kernel32", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern IntPtr VirtualAlloc([In] IntPtr lpAddress, [In] uint dwSize, [In] int flAllocationType, [In] int flProtect);

        [DllImport("Kernel32", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        internal static extern bool VirtualFree([In] IntPtr lpAddress, [In] uint dwSize, [In] int dwFreeType);

        public static class Consts
        {
            public const int MEM_COMMIT = 0x00001000;
            public const int MEM_RESERVE = 0x00002000;
            public const int MEM_RELEASE = 0x8000;
            public const int PAGE_READWRITE = 0x04;
        }
    }
}
