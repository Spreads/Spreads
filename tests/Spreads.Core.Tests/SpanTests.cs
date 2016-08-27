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
using Spreads.Slices;

namespace Spreads.Core.Tests {


    [TestFixture]
    public class SpanTests {


        [Test]
        public unsafe void CouldUseBinarySearchOverSpan() {
            var upper = 8 * 1024;
            var ptr = Marshal.AllocHGlobal(8 * upper);
            var byteBuffer = new byte[8 * upper];
            var intBuffer = new long[upper];

            var ptrSpan = new Span<long>((void*)ptr, upper);
            var byteArraySpan = new Span<long>((object)byteBuffer, UIntPtr.Zero, upper);
            var intArraySpan = new Span<long>(intBuffer);

            var rng = new System.Random();
            var indices = new long[upper];

            var rounds = 20;
            for (int r = 0; r < rounds; r++) {
                Console.WriteLine($"Round: {r} ------------------");
                // indices are the same for all spans
                for (int i = 0; i < upper; i++) {
                    indices[i] = rng.Next(0, upper);
                }

                TestSpanForFillAndBinarySearch(ptrSpan, indices, "PtrSpan");
                //TestSpanForFillAndBinarySearch(byteArraySpan, indices, "ByteArraySpan");
                TestSpanForFillAndBinarySearch(intArraySpan, indices, "ArrSpan");
                TestArrayForFillAndBinarySearch(intBuffer, indices, "Array");

            }

        }

        private void TestSpanForFillAndBinarySearch(Span<long> span, long[] indices, string caseName) {
            var sw = new Stopwatch();
            sw.Start();
            for (int r = 0; r < 10000; r++) {
                for (int i = 0; i < span.Length; i++) {
                    span.SetUnsafe(i, i);
                }
            }
            sw.Stop();
            Console.WriteLine($"Fill {caseName}: \t\t {10000 * span.Length * 0.001 / sw.ElapsedMilliseconds} Mops");

            sw.Restart();
            for (int r = 0; r < 500; r++) {
                for (int i = 0; i < indices.Length; i++) {
                    var target = indices[i];
                    var found = span.BinarySearch(0, span.Length, target, null);
                    if (found != target) Assert.Fail($"Wrong binary search: expected {target}, found {found}");
                }
            }
            sw.Stop();
            Console.WriteLine($"Find {caseName}: \t\t {500 * indices.Length * 0.001 / sw.ElapsedMilliseconds} Mops");
        }

        private void TestArrayForFillAndBinarySearch(long[] array, long[] indices, string caseName) {
            var sw = new Stopwatch();
            sw.Start();
            for (int r = 0; r < 10000; r++) {
                for (int i = 0; i < array.Length; i++) {
                    array[i] = i;
                }
            }
            sw.Stop();
            Console.WriteLine($"Fill {caseName}: \t\t {10000 * array.Length * 0.001 / sw.ElapsedMilliseconds} Mops");

            sw.Restart();
            for (int r = 0; r < 500; r++) {
                for (int i = 0; i < indices.Length; i++) {
                    var target = indices[i];
                    var found = Array.BinarySearch(array, 0, array.Length, target, null);
                    if (found != target) Assert.Fail("Wrong binary search");
                }
            }
            sw.Stop();
            Console.WriteLine($"Find {caseName}: \t\t {500 * indices.Length * 0.001 / sw.ElapsedMilliseconds} Mops");
        }

        


    }
}
