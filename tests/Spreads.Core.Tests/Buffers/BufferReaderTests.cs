// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
    [TestFixture]
    public class BufferReaderTests
    {
        [Test, Explicit("bench")]
        public void CompareWithDirectRead()
        {
            var count = 256 * 1024;
            var bytes = new byte[count];
            var rng = new Random(1);
            rng.NextBytes(bytes);

            long sum;
            var mult = 10_000L;

            for (int i = 0; i < 50; i++)
            {
                CompareWithDirectRead_Array(count, mult, bytes);
                CompareWithDirectRead_BufferReader(count, mult, bytes);
                if (bytes[0] == 1)
                {
                    count = count / 2;
                }
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
        private static void CompareWithDirectRead_Array(int count, long mult, byte[] bytes)
        {
            long sum;
            sum = 0;
            using (Benchmark.Run("byte[]", (count >> 3) * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    var offset = 8;
                    for (int i = 0; i < count >> 3; i++)
                    {
                        sum += Unsafe.ReadUnaligned<long>(ref bytes[offset - 8]);
                        offset += 8;
                    }
                }
            }

            Assert.IsTrue(sum != 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
        private static void CompareWithDirectRead_BufferReader(int count, long mult, byte[] bytes)
        {
            long sum;
            sum = 0;
            using (Benchmark.Run("BurrefReader", (count >> 3) * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    var br = new BufferReader(bytes);
                    for (int i = 0; i < count >> 3; i++)
                    {
                        sum += br.DangerousRead2<long>();
                    }

                    br._offset = 0;
                }
            }
            Assert.IsTrue(sum != 1);
        }
    }
}
