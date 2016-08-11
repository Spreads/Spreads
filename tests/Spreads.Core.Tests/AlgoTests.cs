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
    }
}
