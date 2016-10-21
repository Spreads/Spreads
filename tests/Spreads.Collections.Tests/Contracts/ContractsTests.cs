// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Collections.Tests.Contracts {

    // TODO! Add many more cases and all edge cases to this suite

    // TODO! We must test all base cursors for correctess, preferrably with random inputs
    // and materialized series built manually with LINQ or ad hoc code.
    // Edge cases should be provided from outside, e.g.
    // - empty series
    // - single element series
    // - two element series
    // We could add more and more tests to this suite so that the new tests are invoked for 
    // all existing cursors. New concrete cursor implementations, such as SMA/StDev, must 
    // pass this test suite before adding to the public API, however they could live as internal during WIP

    // NB this suite itself could have bugs, but hopefully its own bugs won't correct any buggy implementation

    /// <summary>
    /// This class tests cursors and series contracts.
    /// It accepts ISeries for testing, a SortedMap with materialized values
    /// and optionally an ephemeral SortedMap with continuous values. Every cursor move
    /// on the ISeries must give the same result as the same move on the materialized SortedMap.
    /// TGV on ISeries must give the same result as a TGV call on the ephemeral SM.
    /// </summary>

    public class ContractsTests<K, V> {
        private readonly IReadOnlyOrderedMap<K, V> _testSeries;
        private readonly SortedMap<K, V> _materializedSeries;
        private readonly SortedMap<K, V> _ephemeralSeries;
        private readonly Random _rng = new Random();


        public ContractsTests(IReadOnlyOrderedMap<K, V> testSeries,
            SortedMap<K, V> materializedSeries,
            SortedMap<K, V> ephemeralSeries = null) {
            if (testSeries == null) throw new ArgumentNullException(nameof(testSeries));
            if (materializedSeries == null) throw new ArgumentNullException(nameof(materializedSeries));
            _testSeries = testSeries;
            _materializedSeries = materializedSeries;
            _ephemeralSeries = ephemeralSeries;

            if (ephemeralSeries != null) {
                var eqc = EqualityComparer<V>.Default;
                // TODO (UX) Spreads signature conflicts with LINQ, easy to fix but not very convenient
                var intersect = materializedSeries.Zip(ephemeralSeries, (l, r) => {
                    if (!eqc.Equals(l, r)) {
                        throw new ArgumentException("materializedSeries and ephemeralSeries contain different values for the same keys");
                    } else {
                        return l;
                    }
                });
                if (intersect.IsEmpty) {
                    foreach (var kvp in materializedSeries.Take(10)) {
                        ephemeralSeries[kvp.Key] = kvp.Value;
                    }
                }
            }
        }


        /// <summary>
        /// If these ones fail, one should consider to kill him/herself and never touch keyboard again!
        /// </summary>
        public void BasicTests() {
            Assert.AreEqual(_materializedSeries.Count, _testSeries.Count());
            Assert.AreEqual(_materializedSeries.IsEmpty, !_testSeries.Any());
            Assert.AreEqual(_materializedSeries.IsReadOnly, _testSeries.IsReadOnly);
            if (_materializedSeries.Count == 0) {
                return;
            }
            Assert.AreEqual(_materializedSeries.First, _testSeries.First());
            Assert.AreEqual(_materializedSeries.Last, _testSeries.Last());

            foreach (var kvp in _materializedSeries) {
                KeyValuePair<K, V> tkvp;
                if (_testSeries.TryFind(kvp.Key, Lookup.EQ, out tkvp)) {
                    Assert.AreEqual(kvp, tkvp);
                }
            }


            for (int i = 0; i < 1000; i++) {
                var idx = _rng.Next(_materializedSeries.Count);
                // TODO (UX) Public cannot get key by index
                var v = _materializedSeries.GetAt(idx);
                var k = _materializedSeries.keys[idx];
                V tv;
                if (_testSeries.TryGetValue(k, out tv)) {
                    Assert.AreEqual(v, tv);
                }
            }
        }




        public void CursorContractsTests() {
            var notEmpty = false;
            /// Independent cursors moves
            var tc = _testSeries.GetCursor();
            var tc2 = _testSeries.GetCursor();
            var mc = _materializedSeries.GetCursor();
            while (mc.MoveNext()) {
                notEmpty = true;
                Assert.IsTrue(tc.MoveNext(), "Independent cursors moves");
                Assert.IsTrue(tc2.MoveNext(), "Independent cursors moves");
                Assert.AreEqual(mc.Current, tc.Current, "Independent cursors moves: kvps are not equal after similar moves");
                Assert.AreEqual(tc.Current, tc2.Current, "Independent cursors moves: kvps are not equal after similar moves");
            }
            // one of the key contract is that MoveNext() does not destroy state after returning false,
            // because we could either spin or switch to MoveNextAsync()
            Assert.IsFalse(mc.MoveNext(), "MN after false MN should be false");
            Assert.IsFalse(tc.MoveNext(), "MN after false MN should be false");
            Assert.IsFalse(tc2.MoveNext(), "MN after false MN should be false");
            if (notEmpty) Assert.AreEqual(tc.Current, tc2.Current, "MN after false MN should be false and keep current key/value");

            /// we must be able to MP after false MN
            while (mc.MovePrevious()) {
                Assert.IsTrue(tc.MovePrevious(), "Independent cursors moves");
                Assert.IsTrue(tc2.MovePrevious(), "Independent cursors moves");
                Assert.AreEqual(mc.Current, tc.Current, "Independent cursors moves: kvps are not equal after similar moves");
                Assert.AreEqual(tc.Current, tc2.Current, "Independent cursors moves: kvps are not equal after similar moves");
            }
            Assert.IsFalse(mc.MovePrevious());
            Assert.IsFalse(tc.MovePrevious());
            Assert.IsFalse(tc2.MovePrevious());
            if (notEmpty) Assert.AreEqual(tc.Current, tc2.Current);


            if (notEmpty) {
                // could move first
                Assert.IsTrue(mc.MoveFirst());
                Assert.IsTrue(tc.MoveFirst());
                Assert.IsTrue(tc2.MoveFirst());
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                Lookup dir = Lookup.EQ;
                // could at the first key
                var key = mc.CurrentKey;
                var firstKey = key;
                Assert.IsTrue(mc.MoveAt(key, dir));
                Assert.IsTrue(tc.MoveAt(key, dir));
                Assert.IsTrue(tc2.MoveAt(key, dir));
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                // could at or after the first key
                dir = Lookup.GE;
                Assert.IsTrue(mc.MoveAt(key, dir));
                Assert.IsTrue(tc.MoveAt(key, dir));
                Assert.IsTrue(tc2.MoveAt(key, dir));
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                // could at first key
                if (_materializedSeries.Count > 1) {
                    dir = Lookup.GT;
                    Assert.IsTrue(mc.MoveAt(key, dir));
                    Assert.IsTrue(tc.MoveAt(key, dir));
                    Assert.IsTrue(tc2.MoveAt(key, dir));
                    Assert.AreEqual(mc.Current, tc.Current);
                    Assert.AreEqual(tc.Current, tc2.Current);

                    dir = Lookup.EQ;
                    // could move at the first key after moving away from it
                    key = firstKey;
                    Assert.IsTrue(mc.MoveAt(key, dir));
                    Assert.IsTrue(tc.MoveAt(key, dir));
                    Assert.IsTrue(tc2.MoveAt(key, dir));
                    Assert.AreEqual(mc.Current, tc.Current);
                    Assert.AreEqual(tc.Current, tc2.Current);
                }



                // could move last
                Assert.IsTrue(mc.MoveLast());
                Assert.IsTrue(tc.MoveLast());
                Assert.IsTrue(tc2.MoveLast());
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                dir = Lookup.EQ;
                // could move at the last key
                key = mc.CurrentKey;
                var lastKey = key;
                Assert.IsTrue(mc.MoveAt(key, dir));
                Assert.IsTrue(tc.MoveAt(key, dir));
                Assert.IsTrue(tc2.MoveAt(key, dir));
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                // could at or before the last key
                dir = Lookup.LE;
                Assert.IsTrue(mc.MoveAt(key, dir));
                Assert.IsTrue(tc.MoveAt(key, dir));
                Assert.IsTrue(tc2.MoveAt(key, dir));
                Assert.AreEqual(mc.Current, tc.Current);
                Assert.AreEqual(tc.Current, tc2.Current);

                // could move before the last key
                if (_materializedSeries.Count > 1) {
                    dir = Lookup.LT;
                    Assert.IsTrue(mc.MoveAt(key, dir));
                    Assert.IsTrue(tc.MoveAt(key, dir));
                    Assert.IsTrue(tc2.MoveAt(key, dir));
                    Assert.AreEqual(mc.Current, tc.Current);
                    Assert.AreEqual(tc.Current, tc2.Current);


                    dir = Lookup.EQ;
                    // could move at the last key after moving away from it
                    key = lastKey;
                    Assert.IsTrue(mc.MoveAt(key, dir));
                    Assert.IsTrue(tc.MoveAt(key, dir));
                    Assert.IsTrue(tc2.MoveAt(key, dir));
                    Assert.AreEqual(mc.Current, tc.Current);
                    Assert.AreEqual(tc.Current, tc2.Current);
                }



            }
        }

        /// <summary>
        /// If series is continuous, TGV must work as expected
        /// </summary>
        public void EphemeralValuesTest() {
            var tc = _testSeries.GetCursor();
            if (tc.IsContinuous && _ephemeralSeries == null)
            {
                Trace.TraceWarning("Privide ephemeral series for continuous test series");
            }
            if (tc.IsContinuous && _ephemeralSeries != null) {
                if (_ephemeralSeries != null) {
                    foreach (var kvp in _ephemeralSeries) {
                        V tv;
                        if (_testSeries.TryGetValue(kvp.Key, out tv)) {
                            Assert.AreEqual(kvp.Value, tv, "TryGetValue on continuous series gives wrong result");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Run all tests
        /// </summary>
        public void Run() {
            BasicTests();
            CursorContractsTests();
            EphemeralValuesTest();
        }
    }
}
