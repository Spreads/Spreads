// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Experimental;
using Spreads.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Collections.Experimental
{
    [TestFixture]
    public unsafe class LRUCacheTests
    {
        public class MyClass : WithId, IEquatable<MyClass>
        {
            public long Id { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

            public bool Equals(MyClass other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Id == other.Id;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MyClass)obj);
            }

            public override int GetHashCode()
            {
                return unchecked((int)Id);
            }
        }

        [Test, Explicit("Benchmark")]
        public void GetSpeed()
        {
            var lru = new LRUCache<MyClass>(37);
            var cd = new ConcurrentDictionary<long, MyClass>();
            var d = new Dictionary<long, MyClass>();

            var size = 50_000;
            var lookups = new int[size / 10];
            var rng = new System.Random(42);

            for (int i = 0; i < size; i++)
            {
                lru[i] = new MyClass { Id = i };
                cd[i] = new MyClass { Id = i };
                d[i] = new MyClass { Id = i };
            }

            for (int i = 0; i < lookups.Length; i++)
            {
                // skew to larger
                lookups[i] = rng.Next(size - lookups.Length + i, size - 1);
            }

            const int count = 10_000_000;

            for (int r = 0; r < 10; r++)
            {
                var sumLru = 0.0;
                using (Benchmark.Run("LRU", count))
                {
                    for (int i = 0; i < count / lookups.Length; i++)
                    {
                        for (int j = 0; j < lookups.Length; j++)
                        {
                            sumLru += lru[lookups[j]].Id;
                        }
                    }
                }

                var sumCd = 0.0;
                using (Benchmark.Run("CD", count))
                {
                    for (int i = 0; i < count / lookups.Length; i++)
                    {
                        for (int j = 0; j < lookups.Length; j++)
                        {
                            sumCd += cd[lookups[j]].Id;
                        }
                    }
                }

                var sumD = 0.0;
                using (Benchmark.Run("Locked D", count))
                {
                    for (int i = 0; i < count / lookups.Length; i++)
                    {
                        for (int j = 0; j < lookups.Length; j++)
                        {
                            sumD += d[lookups[j]].Id;
                        }
                    }
                }
            }

            Benchmark.Dump("Read");
        }
    }
}
