using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using NUnit.Framework;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class BDNImpl : _BDN
    {
        [BenchmarkCategory("Cat1")]
        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int BenchImpl()
        {
            var sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                sum += i;
            }
            return sum;
        }
        
        [BenchmarkCategory("Cat2")]
        [Benchmark]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int BenchImpl2()
        {
            var sum = 0;
            for (int i = 0; i < 1000 * 2; i++)
            {
                sum += i;
            }
            return sum;
        }

        [Test]
        public void RunCat2()
        {
            Run("--anyCategories", "Cat2", "Cat1");
        }
        
        [Test]
        public void RunFilter()
        {
            Run("--filter", "*BenchImpl*");
        }

        public static void RunMe()
        {
            Console.WriteLine("X");
        }
    }
}