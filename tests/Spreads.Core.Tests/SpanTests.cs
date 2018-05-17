// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Spreads.Collections;
using Spreads.Utils;

namespace Spreads.Tests
{
    [TestFixture]
    public class SpanTests
    {
        [Test, Ignore("long running")]
        public unsafe void CouldUseBinarySearchOverSpan()
        {
            var upper = 8 * 1024;
            var ptr = Marshal.AllocHGlobal(8 * upper);
            var byteBuffer = new byte[8 * upper];
            var intBuffer = new long[upper];

            var ptrSpan = new Span<long>((void*)ptr, upper);
            //var byteArraySpan = new Span<long>(byteBuffer, UIntPtr.Zero, upper);
            var intArraySpan = new Span<long>(intBuffer);

            var rng = new System.Random();
            var indices = new long[upper];

            var rounds = 20;
            for (int r = 0; r < rounds; r++)
            {
                Console.WriteLine($"Round: {r} ------------------");
                // indices are the same for all spans
                for (int i = 0; i < upper; i++)
                {
                    indices[i] = rng.Next(0, upper);
                }

                TestSpanForFillAndBinarySearch(ptrSpan, indices, "PtrSpan");
                //TestSpanForFillAndBinarySearch(byteArraySpan, indices, "ByteArraySpan");
                TestSpanForFillAndBinarySearch(intArraySpan, indices, "ArrSpan");
                TestArrayForFillAndBinarySearch(intBuffer, indices, "Array");
            }
        }

        private void TestSpanForFillAndBinarySearch(Span<long> span, long[] indices, string caseName)
        {
            var sw = new Stopwatch();
            sw.Start();
            for (int r = 0; r < 10000; r++)
            {
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = i;
                }
            }
            sw.Stop();
            Console.WriteLine($"Fill {caseName}: \t\t {10000 * span.Length * 0.001 / sw.ElapsedMilliseconds} Mops");

            sw.Restart();
            for (int r = 0; r < 500; r++)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    var target = indices[i];
                    var found = span.BinarySearch(0, span.Length, target, KeyComparer<long>.Default);
                    if (found != target) Assert.Fail($"Wrong binary search: expected {target}, found {found}");
                }
            }
            sw.Stop();
            Console.WriteLine($"Find {caseName}: \t\t {500 * indices.Length * 0.001 / sw.ElapsedMilliseconds} Mops");
        }

        private void TestArrayForFillAndBinarySearch(long[] array, long[] indices, string caseName)
        {
            var sw = new Stopwatch();
            sw.Start();
            for (int r = 0; r < 10000; r++)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = i;
                }
            }
            sw.Stop();
            Console.WriteLine($"Fill {caseName}: \t\t {10000 * array.Length * 0.001 / sw.ElapsedMilliseconds} Mops");

            sw.Restart();
            for (int r = 0; r < 500; r++)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    var target = indices[i];
                    var found = Array.BinarySearch(array, 0, array.Length, target, null);
                    if (found != target) Assert.Fail("Wrong binary search");
                }
            }
            sw.Stop();
            Console.WriteLine($"Find {caseName}: \t\t {500 * indices.Length * 0.001 / sw.ElapsedMilliseconds} Mops");
        }

        [Test, Ignore("long running")]
        public void OffsetTests()
        {
            var array = Enumerable.Range(0, 32 * 1024).ToArray();
            var sw = new Stopwatch();
            long sum = 0;
            var rpmax = 10000;
            for (int rounds = 0; rounds < 10; rounds++)
            {
                sw.Restart();
                sum = 0;
                for (int rp = 0; rp < rpmax; rp++)
                {
                    for (long i = 0; i < array.Length; i++)
                    {
                        sum += GetAtOffset(array, (Offset)i);
                    }
                }
                if (sum < 0) throw new Exception(); // use sum after loop
                sw.Stop();
                Console.WriteLine($"Offset: {sw.ElapsedMilliseconds}");

                sw.Restart();

                for (int rp = 0; rp < rpmax; rp++)
                {
                    for (long i = 0; i < array.Length; i++)
                    {
                        sum += GetAtIndex(array, i);
                    }
                }
                sw.Stop();
                if (sum < 0) throw new Exception(); // use sum after loop
                Console.WriteLine($"Index: {sw.ElapsedMilliseconds}");

                sw.Restart();
                sum = 0;
                for (int rp = 0; rp < rpmax; rp++)
                {
                    for (long i = 0; i < array.Length; i++)
                    {
                        sum += array[i];
                    }
                }
                if (sum < 0) throw new Exception(); // use sum after loop
                sw.Stop();
                Console.WriteLine($"Direct: {sw.ElapsedMilliseconds}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetAtIndex(int[] array, long index)
        {
            return array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetAtOffset(int[] array, Offset offset)
        {
            return array[(long)offset];
        }
    }
}