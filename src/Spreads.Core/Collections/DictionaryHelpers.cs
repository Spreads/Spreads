//using System;
//using System.Collections.Generic;
//using System.Diagnostics;

//namespace Spreads.Experimental.Collections.Generic
//{
//    [Obsolete("It's overengineering. Default implementation is good enough. Just set big capacity")]
//    public static class DictionaryHelpers {
//        private const int MaxGen = 20;

//        private static uint[] Primes = new uint[] {
//            1,
//            2,
//            3,
//            7,
//            13,
//            31,
//            61,
//            127,
//            251,
//            509,
//            1021,
//            2039,
//            4093,
//            8191,
//            16381,
//            32749,
//            65521,
//            131071,
//            262139,
//            524287,
//            1048573,
//            2097143,
//            4194301,
//            8388593,
//            16777213,
//            33554393,
//            67108859,
//            134217689,
//            268435399,
//            536870909,
//            1073741789,
//            2147483647,
//            4294967291,
//        };

//        // Total size at generation
//        private static uint[] GenSizes = new uint[]
//        {
//            1,
//            3,
//            7,
//            15,
//            31,
//            63,
//            127,
//            255,
//            511,
//            1023,
//            2047,
//            4095,
//            8191,
//            16383,
//            32767,
//            65535,
//            131071,
//            262143,
//            524287,
//            1048575,
//            2097151,
//            4194303,
//            8388607,
//            16777215,
//            33554431,
//            67108863,
//            134217727,
//            268435455,
//            536870911,
//            1073741823,
//            2147483647,
//            4294967295,
//        };

//        public static List<T> Grow<T>(this List<T> list, int newSize) {
//            var newEntries = new List<T>(newSize);
//            for (int i = 0; i < list.Count; i++) {
//                newEntries[i] = list[i];
//            }
//            return newEntries;
//        }

//        public static List<T> GrowGen<T>(this List<T> list, int newGeneration)
//        {
//            long newSize;
//            if (newGeneration > MaxGen)
//            {
//                newSize = list.Count + (1L << MaxGen);
//            }
//            Trace.Assert(list.Count == GenSize(newGeneration - 1));
            
//            newSize = list.Count + (1L << newGeneration);
//            var newEntries = new List<T>((int)newSize);
//            for (int i = 0; i < list.Count; i++) {
//                newEntries[i] = list[i];
//            }
//            return newEntries;
//        }

//        public static long GenSize(int generation) {
//            if (generation > MaxGen) {
//                var delta = generation - MaxGen;
//                // after max gen we allocate by (1L << MaxGen) increments
//                var size = GenSizes[MaxGen] + (1L << MaxGen) * delta;
//                return size;
//            }
//            return GenSizes[generation];
//        }

//        public static long Bucket(int hashCode, int generation) {
//            if (generation > MaxGen) {
//                // After max gen we scatter bukets evenly
//                return hashCode % Primes[MaxGen + 1];
//            }
//            var bucketWithinIncrement = hashCode%Primes[generation];
//            var absoluteBucketPosition = GenSizes[generation - 1] + bucketWithinIncrement;
//            return absoluteBucketPosition;
//        }
//    }
//}