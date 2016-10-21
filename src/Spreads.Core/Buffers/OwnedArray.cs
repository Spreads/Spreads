// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;

namespace Spreads.Buffers {
    // TODO look closer to Memory<T> and other types from CoreFxLab, maybe we do not need our ones

    public struct OwnedArray<T> : IDisposable {
        private Counter Counter { get; }
        public T[] Array { get; }

        public OwnedArray(int minLength, bool requireExactSize = true) :
            this(Impl.ArrayPool<T>.Rent(minLength, requireExactSize)) {
        }

        public OwnedArray(T[] array) {
            Array = array;
            Counter = new Counter(1);
        }

        public int RefCount => Counter.Value;

        public OwnedArray<T> Rent() {
            Counter.Increment();
            return this;
        }

        public int Return() {
            var remaining = Counter.Decrement();
            if (remaining == 0) {
                Impl.ArrayPool<T>.Return(Array);
            }
            return remaining;
        }

        public void Dispose() {
            Return();
        }
    }
}