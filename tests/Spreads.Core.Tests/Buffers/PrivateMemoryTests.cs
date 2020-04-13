// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ObjectLayoutInspector;
using Spreads.Native;
using Spreads.Threading;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class PrivateMemoryTests
    {
        [Test]
        public void PrivateMemoryLayout()
        {
            TypeLayout.PrintLayout<PrivateMemory<long>>(recursively: false);
            TypeLayout.PrintLayout<PrivateMemory<byte>>(recursively: false);
            TypeLayout.PrintLayout<PrivateMemory<object>>(recursively: false);
            var layout = TypeLayout.GetLayout<PrivateMemory<long>>();
        }

        [StructLayout(LayoutKind.Sequential, Size = 5, Pack = 1)]
        private struct Size5
        {
            public int Int;
            public byte Byte;
        }

        [Test]
        public void CtorLengthEqualsToGoodSize()
        {
            // using var pm1 = PrivateMemory<byte>.Create(160);

            for (int length = 16; length < 1024 * 1024; length += 128)
            {
                using var pm = PrivateMemory<Size5>.Create(length);
                // Assert.AreEqual(Mem.GoodSize((UIntPtr) BitUtil.Align(length, 64)), pm.Length);
                Console.WriteLine($"{length:N0} -> {Mem.GoodSize((UIntPtr) length)} -> {pm.Length:N0} -> {pm.Length * Unsafe.SizeOf<Size5>():N0}");
            }
        }

        [Test]
        public void CtorDoesntThrowsOnBadLength()
        {
            var length = -1;
            using var pm = PrivateMemory<byte>.Create(-1);
            Assert.AreEqual((int)Mem.GoodSize((UIntPtr) Settings.MIN_POOLED_BUFFER_LEN), pm.Length);
            Console.WriteLine(pm.Length);
        }

        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void CouldCreateDisposePrivateMemory()
        {
            var count = TestUtils.GetBenchCount();

            var init = PrivateMemory<byte>.Create(64 * 1024);
            PrivateMemory<byte>[] items = new PrivateMemory<byte>[count];
            for (int _ = 0; _ < 20; _++)
            {
                using (Benchmark.Run("CreateDispose", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        items[i] = PrivateMemory<byte>.Create(64 * 1024);
                        items[i].Dispose();
                    }
                }
                // 

                // using (Benchmark.Run("Dispose", count))
                // {
                //     for (int i = 0; i < count; i++)
                //     {
                //         items[i].Dispose();
                //     }
                // }
            }

            init.Dispose();
            Benchmark.Dump();
            // Mem.Collect(true);
            Mem.StatsPrint();
        }
        
        
        [Test, Explicit]
        public void CouldAllocateAndDropAboveSystemMemory()
        {
            // Test that PM is forcefully finalized
            Settings.PrivateMemoryPerCorePoolSize = 1024 * 1024;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            
            for (int i = 0; i < 100; i++)
            {
                var pm = PrivateMemory<long>.Create(1 * 1024 * 1024);
                Task.Run(async () =>
                {
                    await Task.Yield();
                    pm.CounterRef |= AtomicCounter.Disposed;
                    pm.Free(true);
                    GC.SuppressFinalize(pm);
                }).Wait();
                
                
                // GC.AddMemoryPressure(1 * 1024 * 1024);
                // pm.Dispose();
                // GC.Collect();
            }

            Console.WriteLine("Done");
        }
    }
}