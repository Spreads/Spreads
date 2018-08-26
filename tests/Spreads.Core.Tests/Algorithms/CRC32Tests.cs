// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms.Hash;
using Spreads.Buffers;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Algorithms
{
    [TestFixture]
    public class CRC32Tests
    {
        [Test, Explicit("long running")]
        public unsafe void CRCBenchmark()
        {
            var lens = new int[] { 1, 2, 3, 4, 7, 8, 9, 15, 16, 31, 32, 63, 64, 255, 256, 511, 512, 1023, 1024 };
            foreach (var len in lens)
            {
                var arr = new byte[len];
                var memory = (Memory<byte>)arr;
                var handle = memory.Pin();
                var ptr = (byte*)handle.Pointer;
                var rng = new Random(42);
                rng.NextBytes(memory.Span);
                var count = 1_000_000_000;
                var cnt = count / len;
                var sum = 0UL;

                {
                    uint digest = 0;

                    using (Benchmark.Run("xxHash", count, true))
                    {
                        
                        for (int i = 0; i < cnt; i++)
                        {
                            digest = XxHash.CalculateHash(ptr, len, digest);
                        }
                    }
                }

                uint crc0, crc1, crc2;

                {
                    uint digest = 0;
                    using (Benchmark.Run("CRC32 Intrinsic", count, true))
                    {
                        for (int i = 0; i < cnt; i++)
                        {
                            digest = Crc32C.CalculateCrc32C(ptr, len, digest);
                        }
                    }
                    crc0 = digest;
                }

                {
                    uint digest = 0;
                    using (Benchmark.Run("CRC32 Managed", count, true))
                    {
                        for (int i = 0; i < cnt; i++)
                        {
                            digest = Crc32C.CalculateCrc32CManaged(ptr, len, digest);
                        }
                    }
                    crc1 = digest;
                }

                {
                    var crc32C = new Ralph.Crc32C.Crc32C();
                    uint digest = 0;
                    using (Benchmark.Run("CRC32 Ralph", count, true))
                    {
                        for (int i = 0; i < cnt; i++)
                        {
                            crc32C.Update(arr, 0, arr.Length);
                            digest = crc32C.GetIntValue();
                        }
                    }

                    crc2 = digest;
                }

                Assert.AreEqual(crc0, crc1);
                Assert.AreEqual(crc0, crc2);

                Benchmark.Dump("LEN: " + len);
            }
            
        }
    }
}
