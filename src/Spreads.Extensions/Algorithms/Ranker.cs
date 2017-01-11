// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Algorithms {

    /// <summary>
    /// Calculate ranks of values without allocations.
    /// </summary>
    public static class Ranker<T> {

        [ThreadStatic]
        private static KV<T, int>[] _sorted;

        [ThreadStatic]
        private static int[] _ranked;

        private static KVKeyComparer<T, int> _kvComparer;
        private static IComparer<T> _comparer;

        /// <summary>
        /// Simple ascending zero-based rank
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ArraySegment<int> SortRank(ArraySegment<T> values, IComparer<T> comparer = null) {
            KVKeyComparer<T, int> kvComparer;
            if (_comparer == null) {
                _comparer = comparer ?? Comparer<T>.Default;
                _kvComparer = new KVKeyComparer<T, int>(_comparer);
                kvComparer = _kvComparer;
            } else if (comparer != null && comparer != _comparer) {
                kvComparer = new KVKeyComparer<T, int>(comparer);
            } else {
                kvComparer = _kvComparer;
            }
            if (_sorted == null || values.Count > _sorted.Length) {
                _sorted = new KV<T, int>[values.Count];
                _ranked = new int[values.Count];
            }
            for (int i = 0; i < values.Count; i++) {
                _sorted[i] = new KV<T, int>(values.Array[values.Offset + i], i);
            }
            // TODO use two arrays instead of the _sorted one and use the pool
            Array.Sort(_sorted, kvComparer);
            for (int i = 0; i < _sorted.Length; i++) {
                _ranked[_sorted[i].Value] = i;
            }
            Array.Clear(_sorted, 0, values.Count);
            return new ArraySegment<int>(_ranked);
        }
    }
}
