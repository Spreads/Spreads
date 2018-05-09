// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.Cursors;
using System;
using System.Linq;

namespace Spreads.Core.Tests.Cursors
{
    [TestFixture]
    public class RangeCursorTests
    {
        [Test]
        public void CouldUseRangeValues()
        {
            SortedMap<int, double> sm = null;

            Assert.Throws<NullReferenceException>(() =>
            {
                var nullRange = sm.Range(0, Int32.MaxValue, true, true);
            });

            var empty = new SortedMap<int, double>();

            var range = empty.Range(0, Int32.MaxValue, true, true);
            Assert.True(range.IsEmpty);
            Assert.False(range.Any());

            var nonEmpty = new SortedMap<int, double>
            {
                {1, 1}
            };
            var range1 = nonEmpty.Range(0, Int32.MaxValue, true, true);
            Assert.True(range1.Any());
        }

        [Test]
        public void CouldReuseRangeValuesCursor()
        {
            var nonEmpty = new SortedMap<int, double>
            {
                {1, 1},
                {2, 2}
            };
            var range1 = nonEmpty.Range(0, Int32.MaxValue, true, true);

            //var e = range1.GetAsyncEnumerator();

            foreach (var keyValuePair in range1)
            {
                Assert.True(keyValuePair.Value > 0);
            }

            Console.WriteLine("Foreach is OK");

            Assert.True(range1.Any());

            Console.WriteLine("Count is OK");

            Assert.True(range1.Any());

            Console.WriteLine("Any is OK");

            Assert.True(range1.First.Value > 0);

            Console.WriteLine("Navigation is OK");
        }

        [Test]
        public void SizeOfRange()
        {
            Console.WriteLine(Unsafe.SizeOf<DateTime>());
            Console.WriteLine(Unsafe.SizeOf<Cursor<DateTime, double>>());
            Console.WriteLine(Unsafe.SizeOf<Opt<DateTime>>());
            Console.WriteLine(Unsafe.SizeOf<Range<DateTime, double, Cursor<DateTime, double>>>());
            Console.WriteLine(Unsafe.SizeOf<Series<DateTime, double, Range<DateTime, double, Cursor<DateTime, double>>>>());
            if (IntPtr.Size == 8)
            {
                Assert.AreEqual(48, Unsafe.SizeOf<Range<DateTime, double, Cursor<DateTime, double>>>(),
                    "If Range internals changed that should be reflected in this test.");
            }
            else
            {
                Assert.AreEqual(40, Unsafe.SizeOf<Range<DateTime, double, Cursor<DateTime, double>>>(),
                    "If Range internals changed that should be reflected in this test.");
            }
        }


        [Test]
        public void CouldUseRangeCursorStruct()
        {
            SortedMap<int, double> sm = null;

            Assert.Throws<NullReferenceException>(() =>
            {
                var nullRange = new Range<int, double, SortedMapCursor<int, double>>(sm.GetEnumerator(), 0, Int32.MaxValue, true, true);
            });

            var empty = new SortedMap<int, double>();

            var rangeCursor = new Range<int, double, SortedMapCursor<int, double>>(empty.GetEnumerator(), 0, Int32.MaxValue, true, true);
            // NB Source just wraps the cursor in a new struct,
            // same as new CursorSeries<int, double, RangeCursor<int, double, SortedMapCursor<int, double>>>(rangeCursor);
            var range = rangeCursor.Source;

            Assert.True(range.IsEmpty);
            Assert.False(range.Any());

            var nonEmpty = new SortedMap<int, double>
            {
                {1, 1}
            };
            var rangeCursor1 = new Range<int, double, SortedMapCursor<int, double>>(nonEmpty.GetEnumerator(), 0, Int32.MaxValue, true, true);
            var range1 = rangeCursor1.Source;

            Assert.True(range1.Any());
        }
    }
}