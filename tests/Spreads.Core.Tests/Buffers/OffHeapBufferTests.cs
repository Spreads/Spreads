// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class OffHeapBufferTests
    {
        [Test]
        public void CouldUseOffHeapBuffer()
        {
            Settings.DoAdditionalCorrectnessChecks = false;
            var rounds = 1_000;
            var count = 1_0_000;

            OffHeapBuffer<int> ob = default; // this just works
            // OffHeapBuffer<int> ob = new OffHeapBuffer<int>(count);

            using (Benchmark.Run("OB with grow", count * rounds))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        ob.EnsureCapacity((i + 1) * 4);
                        // Unsafe.WriteUnaligned(ob.Pointer + i, i);
                        ob.DirectBuffer.Write(i * 4, i);
                    }
                }
            }

            using (Benchmark.Run("Span read", count * rounds))
            {
                var sp = ob.Span;
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (sp[i] != i)
                        {
                            Assert.Fail($"sp[i] {sp[i]} != i {i}");
                        }
                    }
                }
            }

            ob.Dispose();
        }

        [Test]
        public void CouldUseOffHeapBufferNonGeneric()
        {
            Settings.DoAdditionalCorrectnessChecks = false;
            var rounds = 1_000;
            var count = 1_0_000;

            OffHeapBuffer ob = default; // this just works
            // OffHeapBuffer ob = new OffHeapBuffer(count * 4);

            var db = ob.DirectBuffer;

            using (Benchmark.Run("OB with grow", rounds * count))
            {
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        ob.EnsureCapacity((i + 1) * 4);
                        // Unsafe.WriteUnaligned(ob.Pointer + i, i);
                        ob.DirectBuffer.Write(i * 4, i);
                    }
                }
            }

            using (Benchmark.Run("Span read", rounds * count))
            {
                var sp = ob.GetSpan<int>();
                for (int r = 0; r < rounds; r++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (sp[i] != i)
                        {
                            Assert.Fail($"sp[i] {sp[i]} != i {i}");
                        }
                    }
                }
            }

            ob.Dispose();
        }
    }
}