// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections.Internal;
using Spreads.Core.Tests.Buffers;
using Spreads.Utils;

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class RingVecIndexingBench
    {
        [OneTimeSetUp]
        public void Init()
        {
            // Console.WriteLine($"Additional checks: {AdditionalCorrectnessChecks.Enabled}");
        }
        
        [Test
#if !DEBUG
         , Explicit("bench")
#endif
        ]
#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void RingVecIndexing()
        {
            var count = (int) TestUtils.GetBenchCount(128 * 1024, 128);
            var rm = PrivateMemory<int>.Create(count);
            var rv = RetainedVec.Create(rm, 0, rm.Length, true);
            for (int i = 0; i < count; i++)
            {
                rv.UnsafeWriteUnaligned((IntPtr) i, i);
            }

            for (int r = 0; r < 10; r++)
            {
                RingVecUtilIndexToOffset(rm);
                DirectPointer(rm);
                Modulo(rm);
                Binary(rm);
            }

            Benchmark.Dump();
            rm.Dispose();
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe void RingVecUtilIndexToOffset(RetainableMemory<int> rm)
        {
            var rounds = 1_000;
            long sum = 0;
            using (Benchmark.Run("RingVecUtil", rm.Length * rounds))
            {
                var head = rm.Length / 2;
                var len = rm.Length;
                var ptr = (int*) rm.Pointer;

                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < len; i++)
                    {
                        sum += ptr[RingVecUtil.IndexToOffset(i, head, len)];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe void DirectPointer(RetainableMemory<int> rm)
        {
            var rounds = 1_000;
            long sum = 0;
            using (Benchmark.Run("DirectPointer", rm.Length * rounds))
            {
                var head = rm.Length / 2;
                var len = rm.Length;
                var ptr = (int*) rm.Pointer;

                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < len; i++)
                    {
                        sum += ptr[i];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe void Modulo(RetainableMemory<int> rm)
        {
            var rounds = 1_000;
            long sum = 0;
            using (Benchmark.Run("Modulo", rm.Length * rounds))
            {
                var head = rm.Length / 2;
                var len = rm.Length;
                var ptr = (int*) rm.Pointer;

                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < len; i++)
                    {
                        sum += ptr[(head + i) % len];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        public unsafe void Binary(RetainableMemory<int> rm)
        {
            var rounds = 1_000;
            long sum = 0;
            using (Benchmark.Run("Binary", rm.Length * rounds))
            {
                var head = rm.Length / 2;
                var len = rm.Length;
                var ptr = (int*) rm.Pointer;

                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < len; i++)
                    {
                        sum += ptr[(head + i) & (len - 1)];
                    }
                }
            }

            if (sum < 1000)
                throw new InvalidOperationException();
        }
    }
}