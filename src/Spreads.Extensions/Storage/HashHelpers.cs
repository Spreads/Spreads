using System;
using System.Diagnostics.Contracts;

namespace Spreads.Storage
{
    internal static class HashHelpers {
        // every next is 2+ times larger than previous
        internal static readonly int[] primes = new int[]
        {
            5,
            11,
            23,
            53,
            113,
            251,
            509,
            1019,
            2039,
            4079,
            8179,
            16369,
            32749,
            65521,
            131063,
            262133,
            524269,
            1048571,
            2097143,
            4194287,
            8388587,
            16777183,
            33554393,
            67108837,
            134217689,
            268435399,
            536870879,
            1073741789,
            MaxPrimeArrayLength
        };


        public static int GetGeneratoin(int min) {
            if (min < 0)
                throw new ArgumentException("Arg_HTCapacityOverflow");
            Contract.EndContractBlock();

            for (int i = 0; i < primes.Length; i++) {
                int prime = primes[i];
                if (prime >= min) return i;
            }
            return -1;
        }

        public static int GetMinPrime() {
            return primes[0];
        }

     
        // This is the maximum prime smaller than Array.MaxArrayLength
        public const int MaxPrimeArrayLength = 2147483629;

    }
}