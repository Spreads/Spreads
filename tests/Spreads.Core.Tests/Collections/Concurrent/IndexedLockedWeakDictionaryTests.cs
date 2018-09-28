// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Concurrent;
using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Collections.Concurrent
{
    [TestFixture]
    public class IndexedLockedWeakDictionaryTests
    {
        public class IndexedStorageValue : IndexedLookup<long, IndexedStorageValue>
        {
            public static long CleanUpCount = 0;

            private readonly long _key;
            private readonly IndexedLockedWeakDictionary<long, IndexedStorageValue> _storage;
            private readonly bool _silent;

            public IndexedStorageValue(long key, IndexedLockedWeakDictionary<long, IndexedStorageValue> storage, bool silent = false)
            {
                _key = key;
                _storage = storage;
                _silent = silent;
            }

            public override IndexedLockedWeakDictionary<long, IndexedStorageValue> Storage => _storage;

            public override long StorageKey => _key;

            public override async Task Cleanup()
            {
                await Task.Delay(1);
                Interlocked.Increment(ref CleanUpCount);
                if (!_silent) { Console.WriteLine($"Cleaned up key: {_key}"); }
            }
        }

        [Test]
        public void ILWDDoesCleanup()
        {
            var count = 100;
            var ilwd = new IndexedLockedWeakDictionary<long, IndexedStorageValue>();

            var wt = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    ilwd.TryAdd(i, new IndexedStorageValue(i, ilwd));
                }
            });

            wt.Wait();

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            while (ilwd.Count > 0)
            {
                Console.WriteLine("Waiting...");
                Thread.Sleep(15);
            }
            Assert.AreEqual(count, IndexedStorageValue.CleanUpCount);
        }

        [Test]
        public void ILWDLookupBench()
        {
            var count = 10_000_000;
            var values = new List<IndexedStorageValue>(count);
            var ilwd = new IndexedLockedWeakDictionary<long, IndexedStorageValue>();

            Task.Run(() =>
            {
                var wt = Task.Run(() =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var value = new IndexedStorageValue(i, ilwd, true);
                        ilwd.TryAdd(i, value);
                        values.Add(value);
                    }
                });

                wt.Wait();

                var rounds = 10L;

                using (Benchmark.Run("By key", rounds * count))
                {
                    for (int r = 0; r < rounds; r++)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            ilwd.TryGetValue(i, out var value);
                            if (i != value.StorageKey)
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }

                using (Benchmark.Run("By index", rounds * count))
                {
                    for (int r = 0; r < rounds; r++)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var value = values[i];

                            ilwd.TryGetByIndex(value.StorageIndex, out var value1);
                            if (value.StorageKey != value1.StorageKey)
                            {
                                Assert.Fail();
                            }
                        }
                    }
                }

                values.Clear();
                values = null;
            }).Wait();

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            while (ilwd.Count > 0)
            {
                Console.WriteLine($"Waiting, cleaned so far: {IndexedStorageValue.CleanUpCount}");
                Thread.Sleep(250);
            }

            Console.WriteLine($"Cleaned total: {IndexedStorageValue.CleanUpCount}");
            Assert.AreEqual(count, IndexedStorageValue.CleanUpCount);
        }

        [Test]
        public void WeakReferenceDefaultVsAllocatedBugCollected()
        {
            GCHandle hd = default;

            var hlist = new List<GCHandle>();

            Task.Run(() =>
            {
                hlist.Add(GCHandle.Alloc(new object(), GCHandleType.Weak));

                //var list = new List<object>();
                //for (int i = 0; i < 100_000_000; i++)
                //{
                //    list.Add(new object());
                //    if (i % 10_000_000 == 0)
                //    {
                //        list.Clear();
                //        GC.Collect(2, GCCollectionMode.Forced, true, true);
                //        GC.WaitForPendingFinalizers();
                //        GC.Collect(2, GCCollectionMode.Forced, true, true);
                //        GC.WaitForPendingFinalizers();
                //        Console.WriteLine(
                //            $"{GC.CollectionCount(0)} - {GC.CollectionCount(1)} - {GC.CollectionCount(2)}");
                //        GC.AddMemoryPressure(1024 * 1024 * 1024);
                //    }
                //}

                //list.Clear();
                //list = null;
            }).Wait();

            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();

            Console.WriteLine(hd.IsAllocated);

            Task.Run(() =>
            {
                Console.WriteLine(hlist[0].Target == null);
                Console.WriteLine(hlist[0].IsAllocated);
            }).Wait();
        }
    }
}
