using System;
using NUnit.Framework;
using Spreads.Utils;
using SpreadsX.Experimental;
using System.Collections.Generic;
using System.Linq;

namespace Spreads.Tests
{
    [TestFixture]
    public class Sandbox
    {
        [Test]
        public void GenericTypeof()
        {
            var s = new Series<int, Series<int, double>>();
            Console.WriteLine(s.Test());
        }

        [Test]
        public void DynamicHack()
        {
            var count = 10000000;
            using (Benchmark.Run("Dynamic", count))
            {
                for (int i = 0; i < count; i++)
                {
                    // TestDynamic.Test();
                }
            }
        }

        [Test]
        public void LinqPerf()
        {
            unchecked
            {
                var count = 10_000_000;
                var list = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    list.Add(i);
                }

                var ilist = (IList<int>) list;

                long sum;
                using (Benchmark.Run("Linq", count))
                {
                    var x = list
                        .Select(x => (long)x * 2)
                        .Where(x => (x & 3) == 0)
                        .Where(x => x > 10)
                        .Where(x => x > 100)
                        .Select(x => x * 10)
                        .Where(x => x > 1000)
                        .Where(x => x > 10000)
                        .Select(x => (long) x)
                        ;
                    sum = list.Sum(x => (long)x * 2);
                }

                Console.WriteLine(sum);
                
                using (Benchmark.Run("Loop", count))
                {
                    sum = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        sum += list[i] * 2;
                    }
                }
                
                Console.WriteLine(sum);
            }
        }
    }

    struct ByRefField
    {
        public int Field;
        public Inner _inner;

        public ByRefField(Inner inner) : this()
        {
            _inner = inner;
        }

        public void Test()
        {
            _inner.SetField(ref Field);
        }

        
    }

    public class Inner
    {
        public void SetField(ref int value)
        {
            value = 1;
        }
    } 
}