// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    // NB for 2 writers, Spreads locking is c. 1250 vs 850, for a single writer Spreads locking is more than 2 times faster than
    // lock{} even with additional work with version/nextVersion increment.
    // In Spreads, single writer is the default case and the main goal is to synchronize it with readers
    // Therefore write locking via Interlocked is more preferable

    // TODO Accurately compare write + read locking using lock{} and Spreads scheme

    [TestFixture]
    public unsafe class LockFreeTests
    {
        //sealed
        public class LockTestSeries : BaseSeries<int, int>
        {
            private object _syncRoot = new object();
            private long _counter;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void DoIncrement()
            {
                _counter++;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Increment()
            {
                // NB methods with such `finally` usage as here are not inlined,
                // therefore such `finally` usage should be only in public API methods, which are OK to be not inlined
                // (except for cursor moves, which are the hottest path)
                // For mutations performance is just fine, we do not use Add/Set when reading history anyways but recreate entire SMs in a single call.
                var v2 = 0L;
                try
                {
                    try { }
                    finally
                    {
                        v2 = this.BeforWrite();
                    }
                    DoIncrement();
                }
                finally
                {
                    AfterWrite(v2, true);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IncrementWithLock()
            {
                lock (_syncRoot)
                {
                    _counter++;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IncrementNoLock()
            {
                _counter++;
            }

            public override IComparer<int> Comparer => throw new NotImplementedException();

            public override bool IsIndexed => throw new NotImplementedException();

            public override bool IsReadOnly => throw new NotImplementedException();

            public override bool IsEmpty => throw new NotImplementedException();

            public override KeyValuePair<int, int> First => throw new NotImplementedException();

            public override KeyValuePair<int, int> Last => throw new NotImplementedException();

            public override IEnumerable<int> Keys => throw new NotImplementedException();

            public override IEnumerable<int> Values => throw new NotImplementedException();

            public override Task<bool> Updated => throw new NotImplementedException();

            public long Counter => _counter;

            public override int GetAt(int idx)
            {
                throw new NotImplementedException();
            }

            public override ICursor<int, int> GetCursor()
            {
                throw new NotImplementedException();
            }

            public override bool TryFind(int key, Lookup direction, out KeyValuePair<int, int> value)
            {
                throw new NotImplementedException();
            }

            public override bool TryGetFirst(out KeyValuePair<int, int> value)
            {
                throw new NotImplementedException();
            }

            public override bool TryGetLast(out KeyValuePair<int, int> value)
            {
                throw new NotImplementedException();
            }

            public override bool TryGetValue(int key, out int value)
            {
                throw new NotImplementedException();
            }
        }

        [Test, Ignore]
        public void CouldUseWriteLockManyTimes()
        {
            for (int r = 0; r < 10; r++)
            {
                CouldUseWriteLock();
            }
        }

        [Test, Ignore]
        public void CouldUseWriteLock()
        {
            var count = 10000000;
            var sw = new Stopwatch();

            sw.Restart();

            var lockTest = new LockTestSeries();

            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    lockTest.Increment();
                }
            });

            //var t2 = Task.Run(() =>
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        lockTest.Increment();
            //    }
            //});

            var t3 = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    lockTest.Increment();
                }
            });
            t3.Wait();

            //t2.Wait();

            t1.Wait();

            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Assert.AreEqual(2 * count, lockTest.Counter);
        }

        [Test, Ignore]
        public void CouldUseSimpleLockManyTimes()
        {
            for (int r = 0; r < 10; r++)
            {
                CouldUseSimpleLock();
            }
        }

        [Test, Ignore]
        public void CouldUseSimpleLock()
        {
            var count = 10000000;
            var sw = new Stopwatch();

            sw.Restart();

            var lockTest = new LockTestSeries();

            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    lockTest.IncrementWithLock();
                }
            });

            //var t2 = Task.Run(() =>
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        lockTest.IncrementWithLock();
            //    }
            //});

            //var t3 = Task.Run(() =>
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        lockTest.IncrementWithLock();
            //    }
            //});

            //t3.Wait();

            //t2.Wait();

            t1.Wait();

            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Assert.AreEqual(1 * count, lockTest.Counter);
        }

        [Test, Ignore]
        public void CouldNotIncrementWithoutLock()
        {
            var count = 10000000;
            var sw = new Stopwatch();

            sw.Restart();

            var lockTest = new LockTestSeries();

            var t1 = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    lockTest.IncrementNoLock();
                }
            });

            var t2 = Task.Run(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    lockTest.IncrementNoLock();
                }
            });
            t2.Wait();

            t1.Wait();

            sw.Stop();
            Console.WriteLine($"Elapsed {sw.ElapsedMilliseconds}");
            Assert.True(2 * count > lockTest.Counter);
        }
    }
}