// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spreads.Collections {

    public class ReverseComparer<T> : IComparer<T> {
        private readonly IComparer<T> _comparer;

        public ReverseComparer(IComparer<T> comparer) {
            _comparer = comparer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y) {
            return -_comparer.Compare(x, y);
        }

        public static IComparer<T> Default => new ReverseComparer<T>(Comparer<T>.Default);
    }
}
