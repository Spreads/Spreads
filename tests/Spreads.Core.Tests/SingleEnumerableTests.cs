using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using System.Runtime.InteropServices;


namespace Spreads.Core.Tests {

    public static class SingleEumerable<T> {
        public static IEnumerable<T> Get(T item) {
            yield return item;
        }
    }


    [TestFixture]
    public class SingleEnumerableTests {

        [Test]
        public void DirectSum() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                sum += i;
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }

        [Test]
        public void SingleEnumerableWithYieldForEachManyTimes()
        {
            for (int i = 0; i < 20; i++)
            {
                SingleEnumerableWithYieldForEach();
            }
        }

        [Test]
        public void SingleEnumerableWithYieldForEach() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                foreach (var single in SingleEumerable<int>.Get(i)) {
                    sum += single;
                }
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }


        [Test]
        public void SingleEnumerableWithYieldLinq() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                sum = SingleEumerable<int>.Get(i).Aggregate(sum, (current, single) => current + single);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }


        [Test]
        public void SingleSequenceStructForEachManyTimes()
        {
            for (int i = 0; i < 20; i++)
            {
                SingleSequenceStructForEach();
            }
        }

        // Fastest among seqs, but still 30x times slower than direct sum
        // 49 mops vs 37 mops, or c.30% faster
        [Test]
        public void SingleSequenceStructForEach() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                foreach (var single in new SingleSequence<int>(i)) {
                    sum += single;
                }
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }

        [Test]
        public void SingleSequenceStructLinq() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                sum = new SingleSequence<int>(i).Aggregate(sum, (current, single) => current + single);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }

        //Enumerable.Repeat("abc",1);
        [Test]
        public void SingleLinqRepeatForEach() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                foreach (var single in Enumerable.Repeat(i, 1)) {
                    sum += single;
                }
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }

        [Test]
        public void SingleLinqRepeatLinq() {
            var sw = new Stopwatch();
            sw.Start();
            long sum = 0;
            for (var i = 0; i < 100000000; i++) {
                sum = Enumerable.Repeat(i, 1).Aggregate(sum, (current, single) => current + single);
            }
            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Console.WriteLine($"Mops {100000.0 / sw.ElapsedMilliseconds * 1.0}");
        }

    }
}
