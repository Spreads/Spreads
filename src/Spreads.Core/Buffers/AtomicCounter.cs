// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Buffers {

    public sealed class Counter {
        private int _value;

        public Counter(int initialValue = 0) {
            _value = initialValue;
        }

        public int Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Interlocked.Add(ref _value, 0); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { Interlocked.Exchange(ref _value, value); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int delta) {
            Interlocked.Add(ref _value, delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Increment() {
            return Interlocked.Increment(ref _value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Decrement() {
            return Interlocked.Decrement(ref _value);
        }
    }
}