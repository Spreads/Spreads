using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class Primes {
        public static bool isPrime(long number) {
            long boundary = (long)Math.Floor(Math.Sqrt(number)) + 1L;

            if (number == 1) return false;
            if (number == 2) return true;

            for (long i = 2L; i <= boundary; ++i) {
                if (number % i == 0L) return false;
            }
            return true;
        }
        [Test]
        public void ClosestPrimesToPosersOfTwo()
        {
            for (int i = 0; i <= 33; i++)
            {
                var powerOfTwo = 1L << i - 1;
                while (!isPrime(powerOfTwo))
                {
                    powerOfTwo = powerOfTwo - 1L;
                }
                //Console.WriteLine($"primes[{i}] = {powerOfTwo}");
                Console.WriteLine($"{powerOfTwo},");
            }
        }
    }
}
