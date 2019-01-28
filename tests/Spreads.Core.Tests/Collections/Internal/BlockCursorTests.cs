// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.DataTypes;
using Spreads.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class BlockCursorTests
    {
        private static BaseContainer<int> CreateIntBaseContainer(int capacity, int length)
        {
            var bc = new BaseContainer<int>();
            var block = new DataBlock();
            var rm = ArrayMemory<int>.Create(Enumerable.Range(0, capacity).ToArray(), externallyOwned: true);
            var vs = VectorStorage.Create(rm, 0, rm.Length, 1);
#pragma warning disable 618
            block._rowIndex = vs;
#pragma warning restore 618
            block.RowLength = length;
            bc.DataBlock = block;
            return bc;
        }

        [Test]
        public void SizeOfBlockCursor()
        {
            var size = Unsafe.SizeOf<BlockCursor<Timestamp>>();
            Console.WriteLine(size);
            if (IntPtr.Size == 8)
            {
                Assert.IsTrue(size == 32);
            }
            else
            {
                Assert.IsTrue(size == 24);
            }
        }

        [Test]
        public void CouldMoveNext()
        {
            var count = 100;
            var len = count / 2;
            var bc = CreateIntBaseContainer(count, len);
            var c = new BlockCursor<int>(bc);

            for (int i = 0; i < len; i++)
            {
                Assert.IsTrue(c.MoveNext());
                Assert.AreEqual(i, c._blockPosition);
            }

            Assert.AreEqual(1 - len, c.Move(long.MinValue, true));
        }

        [Test, Explicit("long running")]
        public void CouldMoveNextBench()
        {
            var count = 1000_000;
            var rounds = 20;
            var mult = 1_00;

            var bcImm = CreateIntBaseContainer(count, count);
            bcImm._flags = new Flags((byte)Mutability.Immutable | (byte)KeySorting.Strong);

            var bcMut = CreateIntBaseContainer(count, count);
            bcMut._flags = new Flags((byte)Mutability.Mutable | (byte)KeySorting.Strong);

            BlockCursor<int>[] useMut = new BlockCursor<int>[count];
            var rng = new Random(42);
            var sm = new SortedMap<int, int>(count);
            for (int i = 0; i < count; i++)
            {
                var x = rng.NextDouble();
                // if ((i & 1) == 0) // this is perfectly predicted on i7-8700, give ~300 MOPS
                if (x > 0.50) // this is not predicted at all, performance drops to ~130MOPS, but if we always use the Sync implementation the perf is the same ~300 MOPS, always NoSync ~360 MOPS
                {
                    useMut[i] = new BlockCursor<int>(bcMut);
                }
                else
                {
                    useMut[i] = new BlockCursor<int>(bcImm);
                }

                sm.Add(i, i);
            }

            for (int r = 0; r < rounds; r++)
            {
                MoveNextBenchBranch(useMut, count, mult);

                MoveNextBenchMut(bcMut, count, mult);

                MoveNextBenchImm(bcImm, count, mult);

                MoveNextBenchSM(sm, count, mult);
            }

            Benchmark.Dump();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void MoveNextBenchSM(SortedMap<int, int> sm, int count, int mult)
        {
            // warm up
            for (int _ = 0; _ < 1; _++)
            {
                var cSM = sm.GetCursor();

                for (int i = 0; i < count; i++)
                {
                    cSM.MoveNext();
                }
            }

            using (Benchmark.Run("SM", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    var cSM = sm.GetCursor();

                    for (int i = 0; i < count; i++)
                    {
                        cSM.MoveNext();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void MoveNextBenchImm(BaseContainer<int> bcImm, int count, int mult)
        {
            // warm up
            for (int _ = 0; _ < 1; _++)
            {
                var cImm = new BlockCursor<int>(bcImm);

                for (int i = 0; i < count; i++)
                {
                    cImm.MoveNext();
                }
            }

            using (Benchmark.Run("Imm", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    var cImm = new BlockCursor<int>(bcImm);

                    for (int i = 0; i < count; i++)
                    {
                        cImm.MoveNext();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void MoveNextBenchMut(BaseContainer<int> bcMut, int count, int mult)
        {
            // warmup
            for (int _ = 0; _ < 1; _++)
            {
                var cMut = new BlockCursor<int>(bcMut);

                for (int i = 0; i < count; i++)
                {
                    cMut.MoveNext();
                }
            }

            using (Benchmark.Run("Mut", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    var cMut = new BlockCursor<int>(bcMut);

                    for (int i = 0; i < count; i++)
                    {
                        cMut.MoveNext();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if NETCOREAPP3_0
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static void MoveNextBenchBranch(BlockCursor<int>[] useMut, int count, int mult)
        {
            // warmup
            for (int _ = 0; _ < 1; _++)
            {
                for (int i = 0; i < useMut.Length; i++)
                {
                    useMut[i].MoveNext();
                }
            }

            using (Benchmark.Run("Branch", count * mult))
            {
                for (int _ = 0; _ < mult; _++)
                {
                    for (int i = 0; i < useMut.Length; i++)
                    {
                        useMut[i].MoveNext();
                    }
                }
            }
        }
    }
}
