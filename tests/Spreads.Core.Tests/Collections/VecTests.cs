// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using ObjectLayoutInspector;
using Spreads.Collections;
using Spreads.Utils;

// ReSharper disable UnusedVariable

namespace Spreads.Core.Tests.Collections
{
    internal sealed class Container
    {
        // only field or byref access keeps perf
        private readonly Vec vec;

        public ref readonly Vec Vec
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref vec;
        }

        public Container(Vec vec)
        {
            this.vec = vec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            return Vec.Get<T>(index);
        }
    }

    internal sealed class Container2
    {
        private Container container;

        public Container Container
        {
            get { return container; }
        }

        public Container2(Container container)
        {
            this.container = container;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            return Container.Get<T>(index);
        }
    }

    [Category("CI")]
    [TestFixture]
    public class VecTests
    {
        [Test]
        public void SizeOfVec()
        {
            TypeLayout.PrintLayout<Vec<int>>();
            TypeLayout.PrintLayout<Vec>();
            // Console.WriteLine(Unsafe.SizeOf<Vec<int>>());
            // >= for x86
            Assert.IsTrue(32 >= Unsafe.SizeOf<Vec<int>>());
            Assert.IsTrue(32 >= Unsafe.SizeOf<Vec>());
            // Console.WriteLine(Unsafe.SizeOf<RuntimeVecInfo>());
            // Assert.AreEqual(24, Unsafe.SizeOf<RuntimeVecInfo>());
        }

        [Test]
        public void CouldUseVec()
        {
            var arr = new[] { 1, 2, 3 };
            var vecT = new Vec<int>(arr);
            var vec = new Vec(arr);

            Assert.AreEqual(2, vecT[1]);
            Assert.AreEqual(2, vec.DangerousGetObject(1));

            vecT[1] = 42;

            // vec[2] = (byte)123; // dynamic cast inside

            Assert.AreEqual(3, vecT.Length);
            Assert.AreEqual(3, vec.Length);

            Assert.AreEqual(42, vecT[1]);
            // Assert.AreEqual(123, vecT[2]);

            Assert.AreEqual(42, vec.DangerousGetObject(1));
            // Assert.AreEqual(123, vec[2]);

            Assert.Throws<IndexOutOfRangeException>(() => { vecT[3] = 42; });

            // TODO uncomment when NuGet is updated, GetRef did not have type check
            //Assert.Throws<IndexOutOfRangeException>(() => { vec.GetRef<int>(3) = 42; });
            //Assert.Throws<InvalidOperationException>(() => { vec.GetRef<long>(2) = 42; });

            Assert.Throws<IndexOutOfRangeException>(() => { Console.WriteLine(vec.Get<int>(3)); });
            Assert.Throws<InvalidOperationException>(() => { Console.WriteLine(vec.Get<long>(2)); });
        }

        [Test]
        public void CouldUseVecSlice()
        {
            var arr = new[] { 1, 2, 3 };
            var vecT = new Vec<int>(arr).Slice(1);
            var vec = new Vec(arr).Slice(1);

            Assert.AreEqual(2, vecT.Length);
            Assert.AreEqual(2, vec.Length);

            Assert.AreEqual(2, vecT[0]);
            Assert.AreEqual(3, vecT[1]);

            Assert.AreEqual(2, vec.DangerousGetObject(0));
            Assert.AreEqual(3, vec.DangerousGetObject(1));

            Assert.IsTrue(vec.As<int>().ReferenceEquals(vecT));

            Assert.IsTrue(vecT.Span.SequenceEqual(vec.AsSpan<int>()));
        }

        [Test]
        public unsafe void ArrayAndPointerCtorsHaveSameBehavior()
        {
            var arr = new int[1000];
            var pin = arr.AsMemory().Pin();

            var vArr = new Vec(arr, 500, 500);
            var vPtr = new Vec((byte*)pin.Pointer + 500 * 4, 500, typeof(int));

            Assert.AreEqual(vArr.Length, vPtr.Length);

            for (int i = 0; i < 1000; i++)
            {
                arr[i] = i;
            }

            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual(vArr.Get<int>(i), vPtr.Get<int>(i));
            }

            pin.Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
                    )]
        [Test, Explicit("long running")]
        public void ForEachBench()
        {
            var count = 10_000_000;
            var arr = new int[count];
            IList arrO = arr;
            var vecT = new Vec<int>(arr);
            var vec = new Vec(arr);
            var cont = new Container(vec);
            var cont2 = new Container2(cont);
            var mem = (Memory<int>)arr;
            var list = new List<int>(arr);

            for (int i = 0; i < count; i++)
            {
                vecT[i] = i;
                //if ((int)vec[i] != vecT[i])
                //{
                //    throw new Exception("(int)vec[i] != vecT[i]");
                //}
            }

            long sum = 0;
            var rounds = 10;
            var mult = 10;

            for (int r = 0; r < rounds; r++)
            {
                //using (Benchmark.Run("Array", count * mult))
                //{
                //    var z = count - 1;
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 1; j < z; j++)
                //        {
                //            sum += arr[j - 1];
                //        }
                //    }
                //}

                //using (Benchmark.Run("ArrayO", count * mult))
                //{
                //    var z = count - 1;
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 1; j < z; j++)
                //        {
                //            sum += (int)arrO[j - 1];
                //        }
                //    }
                //}

                //using (Benchmark.Run("ArrayNoBC", count * mult))
                //{
                //    var z = count - 1;
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 1; j < arr.Length; j++)
                //        {
                //            sum += arr[j] + 1;
                //        }
                //    }
                //}

                //using (Benchmark.Run("List", count * mult))
                //{
                //    var z = count - 1;
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 1; j < z; j++)
                //        {
                //            sum += list[j - 1];
                //        }
                //    }
                //}

                //using (Benchmark.Run("VecT", count * mult))
                //{
                //    for (int m = 0; m < mult; m++)
                //    {
                //        var z = count - 1;
                //        for (int j = 1; j < z; j++)
                //        {
                //            sum += vecT.Get(j - 1);
                //        }
                //    }
                //}

                //using (Benchmark.Run("Span", count * mult))
                //{
                //    for (int m = 0; m < mult; m++)
                //    {
                //        var z = count - 1;
                //        var sp = vecT.Span;
                //        for (int j = 1; j < z; j++)
                //        {
                //            sum += sp[j - 1];
                //        }
                //    }
                //}

                sum = VecGetT_Loop(count, mult, sum, vec);

                //using (Benchmark.Run("Vec", count * mult))
                //{
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 0; j < count; j++)
                //        {
                //            sum += (int)vec.DangerousGet(j);
                //        }
                //    }
                //}

                //using (Benchmark.Run("Memory<T>.Span", count * mult))
                //{
                //    for (int m = 0; m < mult; m++)
                //    {
                //        for (int j = 0; j < count; j++)
                //        {
                //            sum += (int)mem.Span[j];
                //        }
                //    }
                //}
            }

            Benchmark.Dump();
            Console.WriteLine(sum);
        }

        [MethodImpl(MethodImplOptions.NoInlining
#if HAS_AGGR_OPT
                    | MethodImplOptions.AggressiveOptimization
#endif
        )]
        private static long VecGetT_Loop(int count, int mult, long sum, Vec vec)
        {
            using (Benchmark.Run("Vec.Get<T>", count * mult))
            {
                for (int m = 0; m < mult; m++)
                {
                    for (int j = 0; j < count; j++)
                    {
                        sum += vec.Get<int>(j);
                    }
                }
            }

            return sum;
        }
    }
}
