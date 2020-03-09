// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Native;
using Spreads.Serialization;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class RetainableMemoryMemoryAccessBench
    {

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void MemoryFieldBench()
        {
            var count = (int) 4 * 1024; // TestUtils.GetBenchCount(1_000_000);
            var rm = PrivateMemory<int>.Create(count);
            var arr = new int[count];
            DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true), Length = rm.Length};

            for (int i = 0; i < count; i++)
            {
                db.VecStorage.UnsafeWriteUnaligned((IntPtr) i, i);
                arr[i] = i;
            }

            for (int r = 0; r < 10; r++)
            {
                MemoryFieldBench_Field(rm);
                MemoryFieldBench_CreateMem(rm);
            }

            Benchmark.Dump();
            rm.Dispose();
        }
        
        
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public void MemoryFieldBench_Field(RetainableMemory<int> rm)
        {
            var rounds = 100_000_000;
            long sum = 0;
            using (Benchmark.Run("Field", rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    sum += rm.Memory.Length;
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }
        
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public void MemoryFieldBench_CreateMem(RetainableMemory<int> rm)
        {
            var rounds = 100_000_000;
            long sum = 0;
            using (Benchmark.Run("CreateMem", rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    sum += rm.CreateMemory().Length;
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void MemoryAccessBench()
        {
            var count = (int) 4 * 1024; // TestUtils.GetBenchCount(1_000_000);
            var rm = PrivateMemory<int>.Create(count);
            var arr = new int[count];
            DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true), Length = rm.Length};

            for (int i = 0; i < count; i++)
            {
                db.VecStorage.UnsafeWriteUnaligned((IntPtr) i, i);
                arr[i] = i;
            }

            for (int r = 0; r < 10; r++)
            {
                Sum(rm);
                MemoryAccessViaPointer(rm);
                MemoryAccessViaArray(rm, arr);
                // MemoryAccessVecViaMemSpan(rm);
                // MemoryAccessVecViaLocalDangerous(rm);
                // MemoryAccessVecViaLocalUnsafe(rm);
                MemoryAccessVecViaDbVecUnsafe(rm);
                MemoryAccessVecViaDbVecStorageRead(rm);
                // MemoryAccessVecViaDbVecUnsafeX(rm);
                MemoryAccessVecViaDbVecPointer(rm);
                // MemoryAccessVecViaDbVecDangerous(rm);
                MemoryAccessVecViaDbVecStorageUnsafe(rm);
                MemoryAccessVecViaDbVecStorageDangerous(rm);

                Thread.Sleep(100);
            }

            Benchmark.Dump();
            rm.Dispose();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public void Sum(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("Sum (CPU Hz)", rm.Length * rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += i;
                    }
                }
            }

            // if (sum < 1000)
            //     throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public unsafe void MemoryAccessViaPointer(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("Pointer", rm.Length * rounds))
            {
                var ptr = (int*) rm.Pointer;
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += ptr[i];
                    }
                }
            }

            // if (sum < 1000)
            //     throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public unsafe void MemoryAccessViaArray(RetainableMemory<int> rm, int[] arr)
        {
            var rounds = 100_000;
            DataBlockLike db = new DataBlockLike {Rm = rm, arr = arr};
            long sum = 0;
            using (Benchmark.Run("Array", rm.Length * rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < db.arr.Length; i++)
                    {
                        sum += db.arr[i];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaMemSpan(RetainableMemory<int> rm)
        {
            var rounds = 10_000;
            long sum = 0;
            using (Benchmark.Run("MemSpan", rm.Length * rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += rm.Memory.Span[(int) i];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaLocalDangerous(RetainableMemory<int> rm)
        {
            var rounds = 100_00;
            long sum = 0;
            using (Benchmark.Run("LocalDangerous", rm.Length * rounds))
            {
                var vec = rm.GetVec().AsVec();
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < rm.Length; i++)
                    {
                        sum += vec.DangerousGetUnaligned<int>(i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaLocalUnsafe(RetainableMemory<int> rm)
        {
            var rounds = 10_000;
            long sum = 0;
            using (Benchmark.Run("LocalUnsafe", rm.Length * rounds))
            {
                var vec = rm.GetVec().AsVec();
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += vec.UnsafeGetUnaligned<int>((IntPtr) i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecDangerous(RetainableMemory<int> rm)
        {
            long sum = 0;
            var rounds = 10_000;
            using (Benchmark.Run("DbVecDangerous (+)", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec()};
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < rm.Length; i++)
                    {
                        sum += db.Vec.DangerousGetUnaligned<int>(i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecUnsafeX(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecUnsafeX", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true)};
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += db.VecStorage.Vec.UnsafeGetUnalignedX<int>((IntPtr) i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecUnsafe(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecUnsafe", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true)};
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += db.Vec.UnsafeGetUnaligned<int>((IntPtr) i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecStorageRead(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecStorageRead (^)", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true), Length = rm.Length};
                for (long r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < db.Length; i++)
                    {
                        sum += db.VecStorage.UnsafeReadUnaligned<int>((IntPtr) i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public unsafe void MemoryAccessVecViaDbVecPointer(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecPointer (*)", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), Ptr = (int*) rm.Pointer};
                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add<int>(ref Unsafe.AsRef<int>((void*) db.Vec._byteOffset), (IntPtr) i)));
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecStorageUnsafe(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecStorageUnsafe (+)", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true)};

                for (int r = 0; r < rounds; r++)
                {
                    for (long i = 0; i < rm.Length; i++)
                    {
                        sum += db.VecStorage.Vec.UnsafeGetUnaligned<int>((IntPtr) i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
#endif
        public void MemoryAccessVecViaDbVecStorageDangerous(RetainableMemory<int> rm)
        {
            var rounds = 100_000;
            long sum = 0;
            using (Benchmark.Run("DbVecStorageDangerous (_)", rm.Length * rounds))
            {
                DataBlockLike db = new DataBlockLike {Rm = rm, Vec = rm.GetVec().AsVec(), VecStorage = VecStorage.Create(rm, 0, rm.Length, true)};
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < rm.Length; i++)
                    {
                        sum += db.VecStorage.Vec.DangerousGetUnaligned<int>(i);
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }
    }

    internal sealed unsafe class DataBlockLike
    {
        public object Rm;
        public int[] arr;
        public VecStorage VecStorage;
        public Vec Vec;
        public int* Ptr;
        public int Length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>(IntPtr index)
        {
            // return Vec.UnsafeGetUnalignedX<T>(index);
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add(
                    ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(VecStorage._pinnable).Data, VecStorage._byteOffset),
                    index)));
            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add<T>(ref Unsafe.AsRef<T>((void*) VecStorage._byteOffset), index)));
        }
    }

    class TypeInitCache<T>
    {
        public static readonly bool IsReferenceOrContainsReferences = TypeHelper<T>.IsReferenceOrContainsReferences;
    }

    public static class VecExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T UnsafeGetRefX<T>(in this Vec vec, IntPtr index)
        {
            if (TypeHelper<T>.IsReferenceOrContainsReferences)
                return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(vec._pinnable).Data, vec._byteOffset), index);

            return ref Unsafe.Add(ref Unsafe.AsRef<T>((void*) vec._byteOffset), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe T UnsafeGetUnalignedX<T>(in this Vec vec, IntPtr index)
        {
            if (VecTypeHelper<T>.IsReferenceOrContainsReferences)
                return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.As<Pinnable<T>>(vec._pinnable).Data, vec._byteOffset),
                    index)));

            return Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.Add<T>(ref Unsafe.AsRef<T>((void*) vec._byteOffset), index)));
        }
    }
}