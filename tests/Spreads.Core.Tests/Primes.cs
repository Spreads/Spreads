// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class Primes
    {
        public static bool IsPrime(long number)
        {
            long boundary = (long)Math.Floor(Math.Sqrt(number)) + 1L;

            if (number == 1) return false;
            if (number == 2) return true;

            for (long i = 2L; i <= boundary; ++i)
            {
                if (number % i == 0L) return false;
            }
            return true;
        }

        [Test]
        public void ClosestPrimesToPowersOfTwo()
        {
            var list = new List<long>();
            long previous = long.MaxValue;
            for (int i = 33; i >= 3; i--)
            {
                var powerOfTwo = 1L << i - 1;
                while (!(IsPrime(powerOfTwo) && previous / powerOfTwo >= 2))
                {
                    powerOfTwo = powerOfTwo - 1L;
                }
                previous = powerOfTwo;
                list.Add(powerOfTwo);
                //Console.WriteLine($"{powerOfTwo},");
                //Console.WriteLine($"primes[{i}] = {powerOfTwo}");
                int x = 2147483629;
            }
            list.Sort();
            foreach (var powerOfTwo in list)
            {
                Console.WriteLine($"{powerOfTwo},");
            }
        }
    }
}