// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Concurrent;
using Spreads.Collections.Generic;
using Spreads.Utils;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Collections.Concurrent
{
    [TestFixture]
    public class LockedWeakDictionaryTests
    {
        public class Dummy : IStorageIndexed
        {
            public int StorageIndex
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set;
            }
        }

        [Test, Explicit("long running")]
        public void WeakReferenceLookup()
        {
            // How bad is WR lookup

            var d = new FastDictionary<long, object>();
            // var ds = new DictionarySlim<long, object>();
            var lockedWeakDictionary = new LockedWeakDictionary<long>();
            var indexedLockedWeakDictionary = new IndexedLockedWeakDictionary<long, Dummy>();
            var cd = new ConcurrentDictionary<long, object>();
            var wcd = new ConcurrentDictionary<long, WeakReference<object>>();
            //var wcd2 = new ConcurrentDictionary<long, WeakReference>();
            var wcd3 = new ConcurrentDictionary<long, GCHandle>();

            var locker = 0L;

            var count = 1_000;

            for (int i = 0; i < count; i++)
            {
                var obj = (object)new Dummy();
                d.Add(i, obj);
                // ds.GetOrAddValueRef(i) = obj;
                cd.TryAdd(i, obj);
                wcd.TryAdd(i, new WeakReference<object>(obj));
                //wcd2.TryAdd(i, new WeakReference(obj));

                var h = GCHandle.Alloc(obj, GCHandleType.Weak);
                wcd3.TryAdd(i, h);
                lockedWeakDictionary.TryAdd(i, obj);
                // lockedWeakDictionary.TryAdd(i, obj);
            }

            var mult = 100_000;

            for (int r = 0; r < 10; r++)
            {
                //var sum1 = 0.0;
                //using (Benchmark.Run("CD", count * mult))
                //{
                //    for (int i = 0; i < count * mult; i++)
                //    {
                //        if (cd.TryGetValue(i / mult, out var obj))
                //        {
                //            //sum1 += (int)obj;
                //        }
                //        else
                //        {
                //            Assert.Fail();
                //        }
                //    }
                //}

                //var sum2 = 0.0;
                //using (Benchmark.Run("WCD", count * mult))
                //{
                //    // Not so bad, this just needs to save something more in other places

                //    for (int i = 0; i < count * mult; i++)
                //    {
                //        wcd.TryGetValue(i / mult, out var wr);
                //        if (wr.TryGetTarget(out var tgt))
                //        {
                //            //sum2 += (int)tgt;
                //        }
                //        else
                //        {
                //            Assert.Fail();
                //        }
                //    }
                //}

                //Assert.AreEqual(sum1, sum2);

                //var sum4 = 0.0;
                //using (Benchmark.Run("WCD2", count * mult))
                //{
                //    // Not so bad, this just needs to save something more in other places

                //    for (int i = 0; i < count * mult; i++)
                //    {
                //        wcd2.TryGetValue(i / mult, out var wr2);
                //        if (wr2.Target != null)
                //        {
                //            sum4 += (int)wr2.Target;
                //        }
                //        else
                //        {
                //            Assert.Fail();
                //        }
                //    }
                //}
                //Assert.AreEqual(sum1, sum4);

                //var sum5 = 0.0;
                WCD_H(count, mult, wcd3);

                // Assert.AreEqual(sum1, sum5);

                //var sum3 = 0.0;
                //using (Benchmark.Run("LD", count * mult))
                //{
                //    for (int i = 0; i < count * mult; i++)
                //    {
                //        lock (d)
                //        {
                //            if (d.TryGetValue(i / mult, out var obj))
                //            {
                //                sum3 += (int)obj;
                //            }
                //            else
                //            {
                //                Assert.Fail();
                //            }
                //        }
                //    }
                //}

                var sum6 = 0.0;
                LWD(count, mult, lockedWeakDictionary);

                //var sum7 = 0.0;
                //using (Benchmark.Run("ILWD", count * mult))
                //{
                //    for (int i = 0; i < count * mult; i++)
                //    {
                //        if (indexedLockedWeakDictionary.TryGetValue(i / mult, out var val))
                //        {
                //            //sum7 += (int)val.StorageIndex;
                //        }
                //        else
                //        {
                //            Assert.Fail();
                //        }
                //    }
                //}
            }

            Benchmark.Dump();

            Console.WriteLine(d.Count);
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization| MethodImplOptions.NoInlining)]
#endif
        private static void LWD(int count, int mult, LockedWeakDictionary<long> lockedWeakDictionary)
        {
            using (Benchmark.Run("LWD", count * mult))
            {
                for (int i = 0; i < count * mult; i++)
                {
                    if (lockedWeakDictionary.TryGetValue(i / mult, out var val))
                    {
                        //sum6 += (int)val;
                    }
                    else
                    {
                        Assert.Fail();
                    }
                }
            }
        }

#if NETCOREAPP3_0
        [MethodImpl(MethodImplOptions.AggressiveOptimization| MethodImplOptions.NoInlining)]
#endif
        private static void WCD_H(int count, int mult, ConcurrentDictionary<long, GCHandle> wcd3)
        {
            using (Benchmark.Run("WCD H", count * mult))
            {
                // Not so bad, this just needs to save something more in other places

                for (int i = 0; i < count * mult; i++)
                {
                    if (wcd3.TryGetValue(i / mult, out var wr2) && wr2.Target is Dummy val)
                    {
                    }
                    else
                    {
                        Assert.Fail();
                    }

                    //if (wr2.Target is Dummy val)
                    //{
                    //    //sum5 += val;
                    //}
                }
            }
        }
    }
}
