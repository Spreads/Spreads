// // This Source Code Form is subject to the terms of the Mozilla Public
// // License, v. 2.0. If a copy of the MPL was not distributed with this
// // file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// using NUnit.Framework;
// using Spreads.Utils;
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Runtime.CompilerServices;
// using System.Threading;
// using Spreads.Buffers;
//
// namespace Spreads.Core.Tests.X.Series
// {
//     [Category("CI")]
//     [TestFixture]
//     public class SeriesAppendTests
//     {
//         [Test]
//         [TestCase(5)]
//         [TestCase(500)]
//         [TestCase(50000)]
//         public void CouldAppendSeries(int count)
//         {
//             //var counts = new[] { 50, 50000 };
//             //foreach (var count in counts)
//             {
//                 var sa = new AppendSeries<int, int>();
//
//                 Assert.IsTrue(sa.TryAppend(1, 1));
//                 Assert.AreEqual(1, sa.RowCount, "Row count == 1");
//                 Assert.IsFalse(sa.TryAppend(1, 1));
//
//                 Assert.IsTrue(sa.TryAppend(2, 2));
//
//                 Assert.Throws<KeyNotFoundException>(() =>
//                 {
//                     var _ = sa[0];
//                 });
//
//                 Assert.AreEqual(1, sa[1]);
//                 Assert.AreEqual(2, sa[2]);
//
//                 Assert.AreEqual(2, sa.Count());
//
//                 for (int i = 3; i < count; i++)
//                 {
//                     Assert.IsTrue(sa.TryAppend(i, i));
//                     Assert.AreEqual(i, sa.Last.Present.Value);
//                 }
//
//                 GC.Collect(2, GCCollectionMode.Forced, true, true);
//                 GC.WaitForPendingFinalizers();
//                 GC.Collect(2, GCCollectionMode.Forced, true, true);
//                 GC.WaitForPendingFinalizers();
//
//                 //using (var cursor = sa.GetEnumerator())
//                 //{
//                 //    for (int i = 1; i < count; i++)
//                 //    {
//                 //        Assert.IsTrue(cursor.MoveNext(), $"could MN {i}");
//                 //        Assert.AreEqual(i, cursor.CurrentKey);
//                 //        Assert.AreEqual(i, cursor.CurrentValue);
//                 //    }
//                 //}
//
//                 //using (var cursor = sa.GetEnumerator())
//                 //{
//                 //    for (int i = count - 1; i >= 1; i--)
//                 //    {
//                 //        Assert.IsTrue(cursor.MovePrevious(), $"could MP {i}");
//                 //        Assert.AreEqual(i, cursor.CurrentKey);
//                 //        Assert.AreEqual(i, cursor.CurrentValue);
//                 //    }
//                 //}
//
//                 using (var cursor = sa.GetEnumerator())
//                 {
//                     Assert.IsTrue(cursor.Move(count + 1, false) == 0);
//                     Assert.IsTrue(cursor.State == CursorState.Initialized);
//                 }
//
//                 using (var cursor = sa.GetEnumerator())
//                 {
//                     Assert.AreEqual(count - 1, cursor.Move(count + 1, true));
//                     Assert.IsTrue(cursor.State == CursorState.Moving);
//                     Assert.AreEqual(sa.Last.Present.Key, cursor.CurrentKey);
//                     Assert.AreEqual(sa.Last.Present.Value, cursor.CurrentValue);
//                 }
//
//                 using (var cursor = sa.GetEnumerator())
//                 {
//                     Assert.AreEqual(-(count - 1), cursor.Move(-count - 1, true));
//                     Assert.IsTrue(cursor.State == CursorState.Moving);
//                     Assert.AreEqual(sa.First.Present.Key, cursor.CurrentKey);
//                     Assert.AreEqual(sa.First.Present.Value, cursor.CurrentValue);
//                 }
//
//                 sa.Dispose();
//
//                 GC.Collect(2, GCCollectionMode.Forced, true, true);
//                 GC.WaitForPendingFinalizers();
//                 GC.Collect(2, GCCollectionMode.Forced, true, true);
//                 GC.WaitForPendingFinalizers();
//             }
//         }
//
//         [Test
// #if !DEBUG
//          , Explicit("long running")
// #endif
//         ]
//         public void CouldAppendSeriesBench()
//         {
//             if (AdditionalCorrectnessChecks.Enabled)
//             {
//                 Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
//             }
//
//             int count = (int)TestUtils.GetBenchCount(10_000_000, 100_000);
//             int rounds = (int)TestUtils.GetBenchCount(100, 10);
//
//             var sl = new SortedList<int, int>();
//
//             //var cursor = sa.GetAsyncCursor();
//             //cursor.MoveNextAsync();
//             var di = new Dictionary<int, int>();
//
//             //for (int r = 0; r < rounds; r++)
//             //{
//             //    using (Benchmark.Run("SL.Add", count))
//             //    {
//             //        for (int i = r * count; i < (r + 1) * count; i++)
//             //        {
//             //            if (i == r * count + 3)
//             //            {
//             //                continue;
//             //            }
//
//             //            sl.Add(i, i);
//             //        }
//             //    }
//             //    Console.WriteLine($"Added {((r + 1) * count / 1000000):N}");
//             //}
//
//             //for (int r = 0; r < rounds; r++)
//             //{
//             //    using (Benchmark.Run("DI.Add", count))
//             //    {
//             //        for (int i = r * count; i < (r + 1) * count; i++)
//             //        {
//             //            if (i == r * count + 3)
//             //            {
//             //                continue;
//             //            }
//
//             //            di.Add(i, i);
//             //        }
//             //    }
//             //    Console.WriteLine($"Added {((r + 1) * count / 1000000):N}");
//             //}
//
//             BufferPool<int>.MemoryPool.PrintStats();
//
//             for (int _ = 0; _ < 2; _++)
//             {
//                 var sa = new AppendSeries<int, int>();
//
//                 for (int r = 0; r < rounds; r++)
//                 {
//                     using (Benchmark.Run("Append", count))
//                     {
//                         for (int i = r * count; i < (r + 1) * count; i++)
//                         {
//                             if (i == r * count + 3)
//                             {
//                                 continue;
//                             }
//
//                             if (!sa.DangerousTryAppend(i, i))
//                             {
//                                 Console.WriteLine("Cannot add " + i);
//                                 return;
//                             }
//                         }
//                     }
//
//                     Console.WriteLine($"Added {((r + 1) * count / 1_000_000):N}");
//                 }
//
//                 Benchmark.Dump();
//
//                 BufferPool<int>.MemoryPool.PrintStats();
//
//                 sa.Dispose();
//
//                 BufferPool<int>.MemoryPool.PrintStats();
//             }
//         }
//
//         [Test
// #if !DEBUG
//          , Explicit("long running")
// #endif
//         ]
//         public void SearchOverLargeSeriesBench()
//         {
//             if (AdditionalCorrectnessChecks.Enabled)
//             {
//                 Console.WriteLine("AdditionalCorrectnessChecks.Enabled");
//             }
//
// #if DEBUG
//             var counts = new[] { 10, 100, 1000 };
//
// #else
//             var counts = new[] { 10, 100, 1000, 10_000, 20_000, 40_000, 100_000, 1_000_000, 10_000_000 };
//             //var counts = new[] { 1_000_000 };
//
// #endif
//             foreach (var count in counts)
//             {
//                 long rounds = TestUtils.GetBenchCount(10, 1);
//
//                 var sa = new AppendSeries<int, int>();
//                 var sl = new SortedList<int, int>();
//                 var dict = new Dictionary<int, int>();
//
//                 for (int i = 0; i < count; i++)
//                 {
//                     if (i == 3)
//                     {
//                         continue;
//                     }
//
//                     if (!sa.DangerousTryAppend(i, i))
//                     {
//                         Assert.Fail("Cannot add " + i);
//                     }
//
//                     sl.Add(i, i);
//                     dict.Add(i, i);
//                 }
//
//                 var mult = Math.Max(1, 1_00_000 / count);
//
//                 if (count < 20000)
//                 {
//                     mult *= 10;
//                 }
//
//                 for (int r = 0; r < rounds; r++)
//                 {
//                     AppendSeriesTgvBench(count, mult, sa);
//                     SortedListTgvBench(count, mult, sl);
//                     DictionaryTgvBench(count, mult, dict);
//                 }
//                 sa.Dispose();
//             }
//
//             Benchmark.Dump();
//         }
//
//         [MethodImpl(MethodImplOptions.NoInlining
// #if NETCOREAPP3_0
//                     | MethodImplOptions.AggressiveOptimization
// #endif
//         )]
//         private static void AppendSeriesTgvBench(int count, int mult, Series<int, int> sa)
//         {
//             using (Benchmark.Run($"AS {count:N}", count * mult))
//             {
//                 for (int _ = 0; _ < mult; _++)
//                 {
//                     for (int i = 0; i < count; i++)
//                     {
//                         if (!sa.TryGetValue(i, out var val) || val != i)
//                         {
//                             if (i != 3)
//                             {
//                                 Assert.Fail($"!sa.TryGetValue(i, out var val) || val {val} != i {i}");
//                             }
//                         }
//                     }
//                 }
//             }
//         }
//
//         [MethodImpl(MethodImplOptions.NoInlining
// #if NETCOREAPP3_0
//                     | MethodImplOptions.AggressiveOptimization
// #endif
//         )]
//         private static void SortedListTgvBench(int count, int mult, SortedList<int, int> sl)
//         {
//             using (Benchmark.Run($"SL {count:N}", count * mult))
//             {
//                 for (int _ = 0; _ < mult; _++)
//                 {
//                     for (int i = 0; i < count; i++)
//                     {
//                         if (!sl.TryGetValue(i, out var val) || val != i)
//                         {
//                             if (i != 3)
//                             {
//                                 Assert.Fail($"!sl.TryGetValue(i, out var val) || val {val} != i {i}");
//                             }
//                         }
//                     }
//                 }
//             }
//         }
//
//         [MethodImpl(MethodImplOptions.NoInlining
// #if NETCOREAPP3_0
//                     | MethodImplOptions.AggressiveOptimization
// #endif
//         )]
//         private static void DictionaryTgvBench(int count, int mult, Dictionary<int, int> dictionary)
//         {
//             using (Benchmark.Run($"DI {count:N}", count * mult))
//             {
//                 for (int _ = 0; _ < mult; _++)
//                 {
//                     for (int i = 0; i < count; i++)
//                     {
//                         if (!dictionary.TryGetValue(i, out var val) || val != i)
//                         {
//                             if (i != 3)
//                             {
//                                 Assert.Fail($"!sl.TryGetValue(i, out var val) || val {val} != i {i}");
//                             }
//                         }
//                     }
//                 }
//             }
//         }
//
//         [Test]
//         public void AddExistingThrowsAndKeepsVersion()
//         {
//             using (var s = new AppendSeries<long, long>())
//             {
//                 s.Append(1, 1);
//                 Assert.AreEqual(0, s.Version);
//                 Assert.AreEqual(0, s.OrderVersion);
//                 Assert.Throws<ArgumentException>(() => s.Append(1, 1));
//                 Assert.AreEqual(0, s.Version);
//                 Assert.AreEqual(0, s.OrderVersion);
//                 Assert.AreEqual(0, s.NextOrderVersion);
//             }
//         }
//
//         [Test]
//         public void CouldMoveAtGe()
//         {
//             using (var s = new AppendSeries<int, int>())
//             {
//                 for (int i = 0; i < 100; i++)
//                 {
//                     s.Append(i, i);
//                 }
//
//                 var c = s.GetCursor();
//
//                 c.MoveTo(-100, Lookup.GE);
//
//                 Assert.AreEqual(0, c.CurrentKey);
//                 Assert.AreEqual(0, c.CurrentValue);
//                 var shouldBeFalse = c.MoveTo(-100, Lookup.LE);
//                 Assert.IsFalse(shouldBeFalse);
//                 c.Dispose();
//             }
//         }
//
//         [Test]
//         public void CouldMoveAtLe()
//         {
//             using (var s = new AppendSeries<long, long>())
//             {
//                 for (long i = int.MaxValue; i < int.MaxValue * 4L; i = i + int.MaxValue)
//                 {
//                     s.Append(i, i);
//                 }
//
//                 var c = s.GetCursor();
//
//                 var shouldBeFalse = c.MoveTo(0, Lookup.LE);
//                 Assert.IsFalse(shouldBeFalse);
//
//                 c.Dispose();
//             }
//         }
//
//         [Test]
//         public void CouldMoveAt()
//         {
//             using (var s = new AppendSeries<int, int>())
//             {
//                 s.Append(1, 1);
//                 s.Append(3, 3);
//                 s.Append(5, 5);
//
//                 Assert.AreEqual(5, s.LastValueOrDefault);
//                 var c = s.GetCursor();
//
//                 c.MoveTo(-100, Lookup.GE);
//
//                 Assert.AreEqual(1, c.CurrentKey);
//                 Assert.AreEqual(1, c.CurrentValue);
//
//                 var shouldBeFalse = c.MoveTo(-100, Lookup.LE);
//                 Assert.IsFalse(shouldBeFalse);
//
//                 c.Dispose();
//             }
//         }
//
//         [Test]
//         public void CouldEnumerateGrowingSeries()
//         {
//             var count = 1_000_000;
//             using (var s = new AppendSeries<DateTime, double>())
//             {
//                 var c = s.GetCursor();
//
//                 for (int i = 0; i < count; i++)
//                 {
//                     s.Append(DateTime.UtcNow.Date.AddSeconds(i), i);
//                     c.MoveNext();
//                     Assert.AreEqual(i, c.CurrentValue);
//                 }
//
//                 c.Dispose();
//             }
//         }
//
//         [Test, Ignore("manual benchmark, needs input to terminate")]
//         public void SpinningReadWhileWriteBenchmark()
//         {
//             Console.WriteLine($"Additional checks: {AdditionalCorrectnessChecks.Enabled}");
//
//             var s = new AppendSeries<long, long>(new MovingWindowOptions<long>(1000));
//
//             var monitor = new TestUtils.ReaderWriterCountersMonitor();
//
//             var countDown = new CountdownEvent(1);
//
//             var writeTask = new Thread(() =>
//             {
//                 try
//                 {
//                     countDown.Wait();
//                     while (monitor.IsRunning)
//                     {
//                         var value = (int)monitor.IncrementWrite();
//                         s.DangerousTryAppend(value, value);
//                     }
//                 }
//                 catch (Exception e)
//                 {
//                     Console.WriteLine("Writer:" + e);
//                     throw;
//                 }
//             });
//
//             writeTask.Start();
//
//             var readTask = new Thread(() =>
//             {
//                 var c = s.GetCursor();
//                 try
//                 {
//                     countDown.Signal();
//                     var i = 0L;
//
//                     while (monitor.IsRunning)
//                     {
//                         var sw = new SpinWait();
//                         while (!c.MoveNext())
//                         {
//                             //Thread.SpinWait(10000000);
//                             sw.SpinOnce();
//                             //if (sw.NextSpinWillYield)
//                             //{
//                             //    sw.Reset();
//                             //}
//                         }
//
//                         i++;
//                         if (c.CurrentKey != i || c.CurrentValue != i)
//                         {
//                             throw new ThrowHelper.AssertionFailureException($"c.CurrentKey [{c.CurrentKey}] != i [{i}] || c.CurrentValue [{c.CurrentValue}] != i [{i}]");
//                         }
//
//                         monitor.IncrementRead();
//                     }
//                 }
//                 catch (Exception e)
//                 {
//                     Console.WriteLine("Reader:" + e);
//                     throw;
//                 }
//                 c.Dispose();
//             });
//             readTask.Start();
//
//             Console.ReadLine();
//             monitor.Dispose();
//
//             writeTask.Join();
//             readTask.Join();
//
//             s.Dispose();
//         }
//         
//         
//         [MethodImpl(MethodImplOptions.NoInlining
// #if NETCOREAPP3_0
//                     | MethodImplOptions.AggressiveOptimization
// #endif
//         )]
//         [Test]
//         public void SortedListReverseInsertBench()
//         {
//             var sl = new SortedList<long,long>();
//             var count = 2048;
//             var mult = 100;
//             for (int i = count; i > 0; i--)
//             {
//                 sl.Add(i,i);
//             }
//             sl.Clear();
//             using (Benchmark.Run($"SL {count:N}", count * mult))
//             {
//                 long sum = 0;
//                 for (int _ = 0; _ < mult; _++)
//                 {
//                     for (int i = count; i > 0; i--)
//                     {
//                         // sum += sl[i];
//                         sl.Add(i,i);
//                     }
//                     sl.Clear();
//                 }
//             }
//         }
//
//         
//         public class  TestX
//         {
//             public object this[Func<TestX, bool> func]
//             {
//                 get => null;
//
//             }
//         }
//     }
// }
