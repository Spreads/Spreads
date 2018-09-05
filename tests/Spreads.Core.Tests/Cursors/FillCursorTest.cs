// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class FillCursorTests
    {
        [Test]
        public void CouldFillValues()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 1 },
                { 3, 3 },
                { 5, 5 }
            };

            var c = new Fill<int, double, Cursor<int, double>>(sm.GetWrapper(), 42).Initialize();

            Assert.True(c.MoveNext());
            Assert.AreEqual(1, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveLast());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveAt(4, Lookup.EQ));
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveAt(4, Lookup.LE));
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.TryGetValue(2, out var x));
            Assert.AreEqual(3, c.CurrentValue);
            Assert.AreEqual(42, x);

            var clone = c.Clone();

            Assert.True(c.MoveAt(4, Lookup.GT));
            Assert.AreEqual(5, c.CurrentValue);
            Assert.AreEqual(3, clone.CurrentValue);

            Assert.True(clone.TryGetValue(0, out var y));
            Assert.AreEqual(42, y);
        }

        [Test]
        public void CouldFillThenMapValues()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 0 },
                { 3, 2 },
                { 5, 4 }
            };

            var fc = new Fill<int, double, Cursor<int, double>>(sm.GetWrapper(), 41).Initialize();
            var c = new Map<int, double, double, Fill<int, double, Cursor<int, double>>>(fc, v => v + 1).Initialize();

            Assert.True(c.MoveNext());
            Assert.AreEqual(1, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveLast());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveAt(4, Lookup.EQ));
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveAt(4, Lookup.LE));
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.TryGetValue(2, out var x));
            Assert.AreEqual(3, c.CurrentValue);
            Assert.AreEqual(42, x);

            var clone = c.Clone();

            Assert.True(c.MoveAt(4, Lookup.GT));
            Assert.AreEqual(5, c.CurrentValue);
            Assert.AreEqual(3, clone.CurrentValue);

            Assert.True(clone.TryGetValue(0, out var y));
            Assert.AreEqual(42, y);
        }

        [Test]
        public void CouldFillThenAddValues()
        {
            var sm = new SortedMap<int, double>
            {
                { 1, 0 },
                { 3, 2 },
                { 5, 4 }
            };



            var src = (sm.GetEnumerator() as ISpecializedCursor<int, double, SortedMapCursor<int, double>>).Source;
            var filled = src.Fill(0);
            var filled2 = (src as ISpecializedSeries<int, double, SortedMapCursor<int, double>>).Fill(0);
            var filled3 = sm.Fill(0);

            var fc = new Fill<int, double, Cursor<int, double>>(sm.GetWrapper(), 41).Initialize();
            var c = (fc.Source + 1).GetEnumerator();

            Assert.True(c.MoveNext());
            Assert.AreEqual(1, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveNext());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveLast());
            Assert.AreEqual(5, c.CurrentValue);

            Assert.False(c.MoveAt(4, Lookup.EQ));
            Assert.AreEqual(5, c.CurrentValue);

            Assert.True(c.MoveAt(4, Lookup.LE));
            Assert.AreEqual(3, c.CurrentValue);

            Assert.True(c.TryGetValue(2, out var x));
            Assert.AreEqual(3, c.CurrentValue);
            Assert.AreEqual(42, x);

            var clone = c.Clone();

            Assert.True(c.MoveAt(4, Lookup.GT));
            Assert.AreEqual(5, c.CurrentValue);
            Assert.AreEqual(3, clone.CurrentValue);

            Assert.True(clone.TryGetValue(0, out var y));
            Assert.AreEqual(42, y);

            c.Dispose();
        }
    }
}