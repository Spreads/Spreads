// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Spreads.Tests.Algorithms
{
    [TestFixture]
    public class AlgoTests
    {
        [Test, Explicit("long running")]
        public unsafe void PrintVectorSizes()
        {
            //Console.WriteLine(Vector<float>.Count);
            //Console.WriteLine(Vector<float>.Count * 4 * 8);
            //Console.WriteLine(Vector<double>.Count);
            //Console.WriteLine(Vector<double>.Count * 8 * 8);
            //Console.WriteLine(Unsafe.SizeOf<Complex>());
            //var v = new Vector<float>();
            //var vs = Vector.AsVectorSingle(new Vector<byte>(1));
            //Console.WriteLine(vs[1]);
            var count = 1000000000;
            var ptr = Marshal.AllocHGlobal(count + 100);
            //var floatVector = Unsafe.AsVector<float>(ptr);
            //Console.WriteLine(floatVector[0]);
            //var x = new Vector<float>()

            Console.WriteLine(ptr.ToInt64() % 32);
            var sw = new Stopwatch();
            double readValue = 0.0;
            for (int r = 0; r < 10; r++)
            {
                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    var target = (void*)(ptr + (i));
                    Unsafe.Write(target, 123.04);
                    readValue = Unsafe.Read<double>(target);
                    //Assert.AreEqual(123.04, readValue);
                }
                sw.Stop();
                Console.WriteLine($"Unaligned: {sw.ElapsedMilliseconds}, {readValue}");

                sw.Restart();
                for (int i = 0; i < count; i++)
                {
                    var target = (void*)(ptr + (i));
                    Unsafe.Write(target, 123.04);
                    readValue = Unsafe.Read<double>(target);
                    //Assert.AreEqual(123.04, readValue);
                }
                sw.Stop();
                Console.WriteLine($"Aligned: {sw.ElapsedMilliseconds}, {readValue}");
            }
            Console.WriteLine(readValue);
        }

        //[Test]
        //[Explicit("long running")]
        //public void SimdVsLoopAddition()
        //{
        //    var sw = new Stopwatch();
        //    for (int round = 0; round < 5; round++)
        //    {
        //        for (int size = 0; size <= 20; size++)
        //        {
        //            var result = new float[(int)Math.Pow(2, size)];
        //            var source = new float[(int)Math.Pow(2, size)];
        //            for (int i = 0; i < source.Length; i++)
        //            {
        //                source[i] = i;
        //            }
        //            GC.Collect(3, GCCollectionMode.Forced, true);
        //            sw.Restart();
        //            for (int r = 0; r < 1000; r++)
        //            {
        //                SimdMath.LoopAdd(null, source, 123.4567f, result, source.Length);
        //            }
        //            sw.Stop();
        //            Console.WriteLine($"Size: {source.Length}, Loop elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

        //            //source = new float[(int)Math.Pow(10, size)];
        //            for (int i = 0; i < source.Length; i++)
        //            {
        //                source[i] = i;
        //            }
        //            GC.Collect(3, GCCollectionMode.Forced, true);
        //            sw.Restart();
        //            for (int r = 0; r < 1000; r++)
        //            {
        //                SimdMath.LoopSafeAdd(null, source, 123.4567f, result, source.Length);
        //            }
        //            sw.Stop();
        //            Console.WriteLine($"Size: {source.Length}, LoopSafe elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

        //            //source = new float[(int)Math.Pow(10, size)];
        //            for (int i = 0; i < source.Length; i++)
        //            {
        //                source[i] = i;
        //            }
        //            GC.Collect(3, GCCollectionMode.Forced, true);

        //            sw.Restart();
        //            for (int r = 0; r < 1000; r++)
        //            {
        //                SimdMath.SIMDAdd(null, source, 123.4567f, result, source.Length);
        //            }
        //            sw.Stop();
        //            Console.WriteLine($"Size: {source.Length}, SIMD elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

        //            Console.WriteLine("----------");
        //        }
        //    }
        //}

        //[Test]
        //[Explicit("long running")]
        //public void SimdVsLoopExp()
        //{
        //    var sw = new Stopwatch();
        //    for (int round = 0; round < 5; round++)
        //    {
        //        for (int size = 0; size <= 15; size++)
        //        {
        //            var result = new double[(int)Math.Pow(2, size)];
        //            var source = new double[(int)Math.Pow(2, size)];
        //            for (int i = 0; i < source.Length; i++)
        //            {
        //                source[i] = i;
        //            }
        //            GC.Collect(3, GCCollectionMode.Forced, true);
        //            sw.Restart();
        //            for (int r = 0; r < 1000; r++)
        //            {
        //                SimdMath.LoopSafeExp(null, source, result, source.Length);
        //            }
        //            sw.Stop();
        //            Console.WriteLine($"Size: {source.Length}, LoopSafe elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

        //            //source = new float[(int)Math.Pow(10, size)];
        //            for (int i = 0; i < source.Length; i++)
        //            {
        //                source[i] = i;
        //            }
        //            GC.Collect(3, GCCollectionMode.Forced, true);

        //            Console.WriteLine("----------");
        //        }
        //    }
        //}

        [Test, Explicit("long running")]
        public unsafe void SimdAddOfEnumerable()
        {
            var sw = new Stopwatch();
            var sum = 0L;
            for (int round = 0; round < 5; round++)
            {
                for (int size = 0; size <= 15; size++)
                {
                    var len = (int)Math.Pow(2, size);
                    var longEnumerable = Enumerable.Range(0, len);

                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    sum = 0L;
                    for (int r = 0; r < 1000; r++)
                    {
                        var e = longEnumerable.Select(x => x + 123).GetEnumerator();
                        while (e.MoveNext())
                        {
                            sum += e.Current;
                        }
                        //sum += longEnumerable.Where(x => true).Sum();
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {len}, LINQ elapsed: {sw.ElapsedTicks}, value: {sum}");

                    GC.Collect(3, GCCollectionMode.Forced, true);
                    var arr = new int[Vector<int>.Count];
                    sw.Restart();
                    sum = 0L;

                    for (int r = 0; r < 1000; r++)
                    {
                        var e = longEnumerable.Select(x => x + 123).GetEnumerator();
                        var c = 0;
                        var state = Vector<int>.Zero;
                        var scalar = new Vector<int>(123);
                        while (e.MoveNext())
                        {
                            if (c < arr.Length)
                            {
                                arr[c] = e.Current;
                                c++;
                            }
                            else
                            {
                                var vector = new Vector<int>(arr);
                                state += (vector + scalar);
                                c = 0;
                            }
                        }
                        for (int i = 0; i < arr.Length; i++)
                        {
                            sum += state[i];
                        }
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {len}, SIMD elapsed: {sw.ElapsedTicks}, value: {sum}");

                    Console.WriteLine("----------");
                }
            }
        }

        [Test, Explicit("long running")]
        public unsafe void SimdUnsafeVector()
        {
            float[] values = new float[1024 * 1024];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = i;
            }
            var step = Vector<float>.Count;
            var sw = new Stopwatch();
            var sum = 0.0f;
            for (int round = 0; round < 10; round++)
            {
                GC.Collect(3, GCCollectionMode.Forced, true);
                sw.Restart();
                for (int x = 0; x < 2000; x++)
                {
                    sum = 0.0f;
                    var sumVector = Vector<float>.Zero;
                    for (int index = 0; index < values.Length; index = index + step)
                    {
                        var vector = new Vector<float>(values, index);
                        sumVector += vector;
                    }
                    for (int i = 0; i < step; i++)
                    {
                        sum += sumVector[i];
                    }
                }

                sw.Stop();
                Console.WriteLine($"Ctor elapsed: {sw.ElapsedTicks}, value: {sum}");

                GC.Collect(3, GCCollectionMode.Forced, true);
                var arr = new int[Vector<int>.Count];
                sw.Restart();
                for (int x = 0; x < 2000; x++)
                {
                    sum = 0.0f;
                    var sumVector = Vector<float>.Zero;
                    fixed (float* ptr = &values[0])
                    {
                        byte* bptr = (byte*)ptr;
                        for (int index = 0; index < values.Length; index = index + step)
                        {
                            var vector = Unsafe.Read<Vector<float>>(bptr + index * 4);
                            sumVector += vector;
                        }
                        for (int i = 0; i < step; i++)
                        {
                            sum += sumVector[i];
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine($"Unsafe elapsed: {sw.ElapsedTicks}, value: {sum}");

                Console.WriteLine("----------");
            }
        }
    }
}