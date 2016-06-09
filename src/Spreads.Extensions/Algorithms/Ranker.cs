/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Collections;

namespace Spreads.Algorithms {

    /// <summary>
    /// Calculate ranks of values without allocations.
    /// </summary>
    public static class Ranker<T> {
        [ThreadStatic]
        private static KV<T, int>[] _sorted;
        [ThreadStatic]
        private static KV<T, int>[] _ranked;
        private static KVKeyComparer<T, int> _kvComparer;
        private static IComparer<T> _comparer;

        /// <summary>
        /// Simple ascending zero-based rank
        /// </summary>
        /// <param name="values"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        public static ArraySegment<KV<T, int>> SortRank(ArraySegment<T> values, IComparer<T> comparer = null) {
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
                _ranked = new KV<T, int>[values.Count];
            }
            for (int i = 0; i < values.Count; i++) {
                _sorted[i] = new KV<T, int>(values.Array[values.Offset + i], i);
            }
            Array.Sort(_sorted, kvComparer);
            for (int i = 0; i < _sorted.Length; i++) {
                _ranked[_sorted[i].Value] = new KV<T, int>(_sorted[i].Key, i);
            }
            return new ArraySegment<KV<T, int>>(_ranked);
        }
    }
}
