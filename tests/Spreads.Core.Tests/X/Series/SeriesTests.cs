// // This Source Code Form is subject to the terms of the Mozilla Public
// // License, v. 2.0. If a copy of the MPL was not distributed with this
// // file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// using NUnit.Framework;
// using ObjectLayoutInspector;
// using System;
// using System.Diagnostics.CodeAnalysis;
// using System.Threading;
//
// namespace Spreads.Core.Tests.Series
// {
//     // [Category("CI")]
//     [TestFixture]
//     // [SuppressMessage("ReSharper", "HeapView.BoxingAllocation")]
//     // [SuppressMessage("ReSharper", "HeapView.DelegateAllocation")]
//     public class SeriesTests
//     {
//         /// <summary>
//         /// m_currentCount is at offset 32 with 12 bytes after in <see cref="SemaphoreSlim"/>. We cannot pad before, but at least 
//         /// </summary>
//         private class PaddedSemaphoreSlim : SemaphoreSlim
//         {
// #pragma warning disable 169
//             private long _padding0;
//             private long _padding1;
//             private long _padding2;
//             private long _padding3;
//             private long _padding4;
//             private long _padding5;
//             private long _padding6;
//             private long _padding7;
// #pragma warning restore 169
//             public PaddedSemaphoreSlim(int initialCount) : base(initialCount)
//             {
//             }
//
//             public PaddedSemaphoreSlim(int initialCount, int maxCount) : base(initialCount, maxCount)
//             {
//             }
//         }
//         // [Test, Explicit("output")]
//         // public void SeriesObjectSize()
//         // {
//         //     TypeLayout.PrintLayout<Series<DateTime, double>>();
//         //     TypeLayout.PrintLayout<PaddedSemaphoreSlim>();
//         // }
//         //
//         // [Test]
//         // [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
//         // public void NewSeries()
//         // {
//         //     var s = new Series<int, int>(Array.Empty<int>(), Array.Empty<int>());
//         //     Assert.AreEqual(s.Mutability, Mutability.ReadOnly);
//         //     Assert.AreEqual(s.KeySorting, KeySorting.Strong);
//         //     Assert.AreEqual(0, s.RowCount.Value);
//         //     s.Dispose();
//         //     Assert.IsTrue(s.IsDisposed);
//         //
//         //     var aps = new AppendSeries<int, int>();
//         //     Assert.AreEqual(aps.Mutability, Mutability.AppendOnly);
//         //     Assert.AreEqual(aps.KeySorting, KeySorting.Strong);
//         //     Assert.AreEqual(0, aps.RowCount.Value);
//         //     aps.Dispose();
//         //     Assert.IsTrue(aps.IsDisposed);
//         //
//         //     var mus = new MutableSeries<int, int>();
//         //     Assert.AreEqual(mus.Mutability, Mutability.Mutable);
//         //     Assert.AreEqual(mus.KeySorting, KeySorting.Strong);
//         //     Assert.AreEqual(0, mus.RowCount.Value);
//         //     mus.MarkAppendOnly();
//         //     mus.MarkAppendOnly(); // ignored, does not throw
//         //     Assert.AreEqual(Mutability.AppendOnly, mus.Mutability);
//         //     mus.MarkReadOnly();
//         //     mus.MarkReadOnly(); // ignored, does not throw
//         //     Assert.AreEqual(Mutability.ReadOnly, mus.Mutability);
//         //     Assert.Throws<InvalidOperationException>(() => { mus.MarkAppendOnly(); });
//         //     mus.Dispose();
//         //     Assert.IsTrue(mus.IsDisposed);
//         // }
//
//         [Test]
//         public void NonInitializedCursorThrowsInvalidOperationException()
//         {
//             SCursor<int, int> c = new SCursor<int, int>();
//             // Assert.Throws<InvalidOperationException>(() => { c.MoveFirst(); });
//             // Assert.Throws<InvalidOperationException>(() => { c.MoveLast(); });
//             // Assert.Throws<InvalidOperationException>(() => { c.MoveNext(); });
//             // Assert.Throws<InvalidOperationException>(() => { c.MovePrevious(); });
//             // Assert.Throws<InvalidOperationException>(() => { c.Move(10, true); });
//             // Assert.Throws<InvalidOperationException>(() => { c.Move(-10, true); });
//             // Assert.Throws<InvalidOperationException>(() => { c.Move(10, false); });
//             // Assert.Throws<InvalidOperationException>(() => { c.Move(-10, false); });
//             // Assert.Throws<InvalidOperationException>(() => { c.MoveAt(10, Lookup.EQ); });
//             // Assert.Throws<InvalidOperationException>(() => { c.Dispose(); });
//             Assert.AreEqual(0, c.CurrentValue);
//             Assert.AreEqual(0, c.CurrentKey);
//             Assert.AreEqual(CursorState.None, c.State);
//             Assert.AreEqual(null, c.AsyncCompleter);
//         }
//
//         // TODO cursor Initialize after disposal
//
//         [Test]
//         public void CouldNotMoveAtOnEmpty()
//         {
//             var s = new Series<int, int>(Array.Empty<int>(), Array.Empty<int>());
//             var c = s.GetCursor();
//             Assert.IsFalse(c.MoveAt(1, Lookup.GT));
//             Assert.IsFalse(c.MoveAt(1, Lookup.GE));
//             Assert.IsFalse(c.MoveAt(1, Lookup.EQ));
//             Assert.IsFalse(c.MoveAt(1, Lookup.LE));
//             Assert.IsFalse(c.MoveAt(1, Lookup.LT));
//             c.Dispose();
//             s.Dispose();
//         }
//
//         [Test, Ignore("TODO")]
//         public void CouldMoveAtOnSingle()
//         {
//             Assert.Fail("Proper MA tests");
//             var s = new AppendSeries<int, int>();
//             s.Append(1, 1);
//             var c = s.GetCursor();
//             Assert.IsFalse(c.MoveAt(1, Lookup.GT));
//             Assert.IsTrue(c.MoveAt(1, Lookup.GE));
//             Assert.IsTrue(c.MoveAt(1, Lookup.EQ));
//             Assert.IsTrue(c.MoveAt(1, Lookup.LE));
//             Assert.IsFalse(c.MoveAt(1, Lookup.LT));
//             c.Dispose();
//             s.Dispose();
//         }
//
//         // [Test, Ignore("TODO")]
//         // [TestCase(2)]
//         // [TestCase(3)]
//         // [TestCase(200)]
//         // [TestCase(Settings.LARGE_BUFFER_LIMIT * 5 + Settings.LARGE_BUFFER_LIMIT / 2)]
//         // public void CouldMoveAtOnNonEmpty(int count)
//         // {
//         //     Assert.Fail("Need a proper MA test");
//         //     var s = new AppendSeries<int, int>();
//         //     for (int i = 1; i <= count; i++)
//         //     {
//         //         s.Append(i, i);
//         //     }
//         //
//         //     var searchValue = count / 2;
//         //
//         //     // TODO all corners all directions and all inner
//         //
//         //     var c = s.GetCursor();
//         //     Assert.IsTrue(c.MoveAt(searchValue, Lookup.GT));
//         //     Assert.AreEqual(searchValue + 1, c.CurrentValue);
//         //     Assert.AreEqual(searchValue + 1, c.CurrentKey);
//         //
//         //     Assert.IsTrue(c.MoveAt(searchValue, Lookup.GE));
//         //     Assert.IsTrue(c.MoveAt(searchValue, Lookup.EQ));
//         //     Assert.IsTrue(c.MoveAt(searchValue, Lookup.LE));
//         //     Assert.IsTrue(c.MoveAt(searchValue, Lookup.LT));
//         //     c.Dispose();
//         //     s.Dispose();
//         // }
//
//         // [Test]
//         // public void CouldNotMoveNextPreviousOnEmpty()
//         // {
//         //     var s = new AppendSeries<int, int>();
//         //     var c = s.GetCursor();
//         //     Assert.IsFalse(c.MoveNext());
//         //     Assert.IsFalse(c.MovePrevious());
//         //     Assert.IsFalse(c.MoveFirst());
//         //     Assert.IsFalse(c.MoveLast());
//         //     s.Append(1, 1);
//         //     Assert.IsTrue(c.MoveNext());
//         //     Assert.IsFalse(c.MovePrevious());
//         //     Assert.IsTrue(c.MoveFirst());
//         //     Assert.IsTrue(c.MoveLast());
//         //
//         //     s.Append(2, 2);
//         //     Assert.IsTrue(c.MoveNext());
//         //     Assert.AreEqual(2, c.CurrentKey);
//         //     Assert.AreEqual(2, c.CurrentValue);
//         //     Assert.IsTrue(c.MovePrevious());
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     Assert.IsTrue(c.MoveFirst());
//         //     Assert.IsTrue(c.MoveLast());
//         //
//         //     c.Dispose();
//         //     s.Dispose();
//         // }
//
//         // [Test]
//         // [TestCase(2)]
//         // [TestCase(200)]
//         // [TestCase(Settings.LARGE_BUFFER_LIMIT * 5 + Settings.LARGE_BUFFER_LIMIT / 2)]
//         // public void CouldMovePreviousFromInitialized(int count)
//         // {
//         //     var s = new AppendSeries<int, int>();
//         //     for (int i = 1; i <= count; i++)
//         //     {
//         //         s.Append(i, i);
//         //     }
//         //
//         //     var c = s.GetCursor();
//         //     Assert.IsTrue(c.MovePrevious());
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //
//         //     c.Dispose();
//         //     s.Dispose();
//         // }
//         //
//         // [Test]
//         // [TestCase(2)]
//         // [TestCase(200)]
//         // [TestCase(Settings.LARGE_BUFFER_LIMIT * 5 + Settings.LARGE_BUFFER_LIMIT / 2)]
//         // public void CouldMovePartialFromInitialized(int count)
//         // {
//         //     var s = new AppendSeries<int, int>();
//         //     for (int i = 1; i <= count; i++)
//         //     {
//         //         s.Append(i, i);
//         //     }
//         //
//         //     SCursor<int, int> c;
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(count, c.Move(count * 2, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(count, c.Move(count * 200, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(count, c.Move(count + 1, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(count, c.Move(count, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     // ---------
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(-count, c.Move(-count * 2, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(-count, c.Move(-count * 200, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(-count, c.Move(-(count + 1), true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.AreEqual(-count, c.Move(-count, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     s.Dispose();
//         // }
//
//         // [Test]
//         // [TestCase(2)]
//         // [TestCase(200)]
//         // [TestCase(Settings.LARGE_BUFFER_LIMIT * 5 + Settings.LARGE_BUFFER_LIMIT / 2)]
//         // public void CouldMovePartialFromMoving(int count)
//         // {
//         //     var s = new AppendSeries<int, int>();
//         //     for (int i = 1; i <= count; i++)
//         //     {
//         //         s.Append(i, i);
//         //     }
//         //
//         //     SCursor<int, int> c;
//         //
//         //     c = s.GetCursor();
//         //     c.MoveNext();
//         //     Assert.AreEqual(count - 1, c.Move(count * 2, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MoveNext();
//         //     Assert.AreEqual(count - 1, c.Move(count * 200, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MoveNext();
//         //     Assert.AreEqual(count - 1, c.Move(count + 1, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MoveNext();
//         //     Assert.AreEqual(count - 1, c.Move(count, true));
//         //     Assert.AreEqual(count, c.CurrentKey);
//         //     Assert.AreEqual(count, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     // ---------
//         //
//         //     c = s.GetCursor();
//         //     c.MovePrevious();
//         //     Assert.AreEqual(-count + 1, c.Move(-count * 2, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MovePrevious();
//         //     Assert.AreEqual(-count + 1, c.Move(-count * 200, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MovePrevious();
//         //     Assert.AreEqual(-count + 1, c.Move(-(count + 1), true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     c.MovePrevious();
//         //     Assert.AreEqual(-count + 1, c.Move(-count, true));
//         //     Assert.AreEqual(1, c.CurrentKey);
//         //     Assert.AreEqual(1, c.CurrentValue);
//         //     c.Dispose();
//         //
//         //     s.Dispose();
//         // }
//
//         // [Test]
//         // [TestCase(1)]
//         // [TestCase(2)]
//         // [TestCase(200)]
//         // [TestCase(Settings.LARGE_BUFFER_LIMIT * 5 + Settings.LARGE_BUFFER_LIMIT / 2)]
//         // public void CouldMoveFirstLast(int count)
//         // {
//         //     var s = new AppendSeries<int, int>();
//         //     for (int i = 1; i <= count; i++)
//         //     {
//         //         s.Append(i, i);
//         //     }
//         //     SCursor<int, int> c;
//         //
//         //     c = s.GetCursor();
//         //     Assert.IsTrue(c.MoveFirst());
//         //     Assert.IsTrue(c.MoveLast());
//         //     c.Dispose();
//         //
//         //     c = s.GetCursor();
//         //     Assert.IsTrue(c.MoveLast());
//         //     Assert.IsTrue(c.MoveFirst());
//         //     c.Dispose();
//         //
//         //     s.Dispose();
//         // }
//     }
// }
