// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Native;
using Spreads.Serialization;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    [Category("Bench")]
    [TestFixture]
    public class RetainableMemoryMemoryAccessBenchBDN : _BDN
    {
        private const int Count = 4 * 1024;
        private volatile int _count;
        private volatile int _idx;
        private long _idxLong;
        private PrivateMemory<int> _rm;
        private int[] _arr;
        private DataBlockLike _db;
        private Vec _vec;

        [GlobalSetup]
        public unsafe void SetUp()
        {
            var rng = new Random();
            _count = Count;
            _idx = rng.Next(_count / 4, _count / 2);
            _idxLong = _idx;
            _rm = PrivateMemory<int>.Create(_count);
            _arr = new int[_count];
            _db = new DataBlockLike
                {Rm = _rm, Vec = _rm.GetVec().AsVec(), RetainedVec = RetainedVec.Create(_rm, 0, _rm.Length, true), Length = _rm.Length, arr = _arr, Ptr = (int*) _rm.Pointer};
            _vec = _rm.GetVec().AsVec();

            for (int i = 0; i < _count; i++)
            {
                _db.RetainedVec.UnsafeWriteUnaligned((IntPtr) i, i + 1);
                _arr[i] = i + 1;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _rm.Dispose();
        }

        protected override IConfig GetStandaloneConfig() => DefaultConfig.Instance.With(Job.ShortRun.WithUnrollFactor(1));

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
            Run("--anyCategories", "MemAccess", "--allStats");
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe int MemoryAccessViaDbPointer()
        {
            return _db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_db.Ptr[_idx]]]]]]]]]];
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe int MemoryAccessViaDbPointerX()
        {
            return Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                    Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                        Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                            Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                    Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                        Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                            Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                                Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add(ref Unsafe.AsRef<int>(_db.Ptr),
                                                    _idx))))))))))))))))))))))))))))));
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessViaDbArray()
        {
            return _db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_db.arr[_idx]]]]]]]]]];
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaMemSpan()
        {
            return _rm.Memory.Span[(int) _idx];
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaLocalDangerous()
        {
            return _vec.DangerousGetUnaligned<int>(_idx);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaLocalUnsafe()
        {
            return _vec.UnsafeGetUnaligned<int>((IntPtr) _idx);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecDangerous()
        {
            return _db.Vec.DangerousGetUnaligned<int>(_idx);
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecUnsafeX()
        {
            return _db.RetainedVec.Vec.UnsafeGetUnalignedX<int>((IntPtr) _idx);
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecUnsafe()
        {
            return _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>(
                (IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>(
                    (IntPtr) _db.Vec.UnsafeGetUnaligned<int>(
                        (IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.Vec.UnsafeGetUnaligned<int>((IntPtr) _idxLong))))))))));
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecStorageRead()
        {
            return _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>(
                (IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>(
                    (IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>(
                        (IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>(
                            (IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _db.RetainedVec.UnsafeReadUnaligned<int>((IntPtr) _idxLong))))))))));
        }

        [Benchmark]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe int MemoryAccessVecViaDbVecPointer()
        {
            return Unsafe.ReadUnaligned<int>(ref Unsafe.As<int, byte>(ref Unsafe.Add<int>(ref Unsafe.AsRef<int>((void*) _db.Vec._byteOffset), (IntPtr) _idx)));
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecStorageUnsafe()
        {
            return _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>(
                (IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>(
                    (IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>(
                        (IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>(
                            (IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _db.RetainedVec.Vec.UnsafeGetUnaligned<int>((IntPtr) _idx))))))))));
        }

        [BenchmarkCategoryAttribute("MemAccess")]
        [Benchmark(OperationsPerInvoke = 10)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int MemoryAccessVecViaDbVecStorageDangerous()
        {
            return _db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(
                _db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(
                    _db.RetainedVec.Vec.DangerousGetUnaligned<int>(
                        _db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(_db.RetainedVec.Vec.DangerousGetUnaligned<int>(_idx))))))))));
        }

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
            Run("-f", "*MemoryField*");
        }

        [Benchmark(OperationsPerInvoke = 1000)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public long MemoryFieldBench_Field()
        {
            var rounds = 1000;
            long sum = 0;

            for (int r = 0; r < rounds; r++)
            {
                sum += _rm.Memory.Length;
            }

            if (sum < 1000)
                throw new InvalidOperationException();
            return sum;
        }

        [Benchmark(OperationsPerInvoke = 1000)]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public long MemoryFieldBench_CreateMem()
        {
            var rounds = 1000;
            long sum = 0;

            for (int r = 0; r < rounds; r++)
            {
                sum += _rm.CreateMemory().Length;
            }

            if (sum < 1000)
                throw new InvalidOperationException();
            return sum;
        }
    }
}