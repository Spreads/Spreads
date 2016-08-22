using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using System.Linq;
using Spreads.Collections;
using Spreads.Algorithms;
using System.Numerics;

namespace Spreads.Core.Tests {


    [TestFixture]
    public class AlgoTests {

        [Test]
        public void SimdVsLoopAddition() {
            var sw = new Stopwatch();
            for (int round = 0; round < 5; round++) {
                for (int size = 0; size <= 20; size++) {
                    var result = new float[(int)Math.Pow(2, size)];
                    var source = new float[(int)Math.Pow(2, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.LoopAdd(null, source, 123.4567f, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, Loop elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

                    //source = new float[(int)Math.Pow(10, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.LoopSafeAdd(null, source, 123.4567f, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, LoopSafe elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");


                    //source = new float[(int)Math.Pow(10, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.YepppAdd(null, source, 123.4567f, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, Yeppp elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");

                    //source = new float[(int)Math.Pow(10, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.SIMDAdd(null, source, 123.4567f, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, SIMD elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");


                    Console.WriteLine("----------");
                }
            }
        }


        [Test]
        public void SimdVsLoopExp() {
            var sw = new Stopwatch();
            for (int round = 0; round < 5; round++) {
                for (int size = 0; size <= 15; size++) {
                    var result = new double[(int)Math.Pow(2, size)];
                    var source = new double[(int)Math.Pow(2, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.LoopSafeExp(null, source, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, LoopSafe elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");


                    //source = new float[(int)Math.Pow(10, size)];
                    for (int i = 0; i < source.Length; i++) {
                        source[i] = i;
                    }
                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    for (int r = 0; r < 1000; r++) {
                        SimdMath.YepppExp(null, source, result, source.Length);
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {source.Length}, Yeppp elapsed: {sw.ElapsedTicks}, last value: {result[source.Length - 1]}");


                    Console.WriteLine("----------");
                }
            }
        }

        [Test]
        public unsafe void SimdAddOfEnumerable() {

            var sw = new Stopwatch();
            var sum = 0L;
            for (int round = 0; round < 5; round++) {
                for (int size = 0; size <= 15; size++) {
                    var len = (int)Math.Pow(2, size);
                    var longEnumerable = Enumerable.Range(0, len);

                    GC.Collect(3, GCCollectionMode.Forced, true);
                    sw.Restart();
                    sum = 0L;
                    for (int r = 0; r < 1000; r++) {
                        var e = longEnumerable.Select(x => x + 123).GetEnumerator();
                        while (e.MoveNext()) {
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
                    
                    for (int r = 0; r < 1000; r++) {
                        var e = longEnumerable.Select(x => x + 123).GetEnumerator();
                        var c = 0;
                        var state = Vector<int>.Zero;
                        var scalar = new Vector<int>(123);
                        while (e.MoveNext()) {
                            if (c < arr.Length) {
                                arr[c] = e.Current;
                                c++;
                            } else {
                                var vector = new Vector<int>(arr);
                                state += (vector + scalar);
                                c = 0;
                            }
                        }
                        for (int i = 0; i < arr.Length; i++) {
                            sum += state[i];
                        }
                    }
                    sw.Stop();
                    Console.WriteLine($"Size: {len}, SIMD elapsed: {sw.ElapsedTicks}, value: {sum}");


                    Console.WriteLine("----------");
                }
                
            }
        }
    }
}
