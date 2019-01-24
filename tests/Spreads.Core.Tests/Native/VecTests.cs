// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Native;
using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable UnusedVariable

namespace Spreads.Core.Tests.Native
{
    [Category("CI")]
    [TestFixture]
    public class VecTests
    {
        [Test]
        public void SizeOfVec()
        {
            // >= for x86
            Assert.IsTrue(24 >= Unsafe.SizeOf<Vec<int>>());
            Assert.IsTrue(24 >= Unsafe.SizeOf<Vec>());
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
            Assert.AreEqual(2, vec[1]);

            vecT[1] = 42;

            vec[2] = (byte)123; // dynamic cast inside

            Assert.AreEqual(3, vecT.Length);
            Assert.AreEqual(3, vec.Length);

            Assert.AreEqual(42, vecT[1]);
            Assert.AreEqual(123, vecT[2]);

            Assert.AreEqual(42, vec[1]);
            Assert.AreEqual(123, vec[2]);

            Assert.Throws<IndexOutOfRangeException>(() => { vecT[3] = 42; });
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

            Assert.AreEqual(2, vec[0]);
            Assert.AreEqual(3, vec[1]);

            Assert.IsTrue(vec.As<int>().ReferenceEquals(vecT));

            Assert.IsTrue(vecT.Span.SequenceEqual(vec.AsSpan<int>()));
        }

        [Test, Explicit("long running")]
        public void ForEachBench()
        {
            var count = 50_000_000;
            var arr = new int[count];
            IList arrO = arr;
            var vecT = new Vec<int>(arr);
            var vec = new Vec(arr);
            var mem = (Memory<int>)arr;
            var list = new List<int>(arr);

            //for (int i = 0; i < count; i++)
            //{
            //    vecT[i] = i;
            //    //if ((int)vec[i] != vecT[i])
            //    //{
            //    //    throw new Exception("(int)vec[i] != vecT[i]");
            //    //}
            //}

            long sum = 0;
            var rounds = 20;
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
                //            sum += vecT.DangerousGet(j - 1);
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

                using (Benchmark.Run("Vec.Get<T>", count * mult))
                {
                    for (int m = 0; m < mult; m++)
                    {
                        for (int j = 0; j < count; j++)
                        {
                            sum += vec.DangerousGet<int>(j);
                        }
                    }
                }

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
    }
}