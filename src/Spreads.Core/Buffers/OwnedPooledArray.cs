// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using System.Buffers;

namespace Spreads.Buffers {

    public sealed class OwnedPooledArray<T> : OwnedMemory<T> {
        // NB In ArrayPool implementation, DefaultMaxNumberOfArraysPerBucket = 50, but MPMCQ requires a power of two
        // OwnedMemory<T> is quite fat and is an object, but has Initialize + Dispose methods that made it suitable for pooling

        // ReSharper disable once StaticMemberInGenericType
        private static readonly MultipleProducerConsumerQueue Pool = new MultipleProducerConsumerQueue(64);

        public new T[] Array => base.Array;

        public static implicit operator T[] (OwnedPooledArray<T> owner) {
            return owner.Array;
        }

        private OwnedPooledArray(T[] array) : base(array, 0, array.Length) {
        }

        protected override void Dispose(bool disposing) {
            // NB this method is called after ensuring that refcount is zero but before
            // cleaning the fields
            BufferPool<T>.Return(Array);
            base.Dispose(disposing);
            Pool.TryEnqueue(this);
        }

        // ReSharper disable once RedundantOverridenMember
        protected override void OnReferenceCountChanged(int newReferenceCount) {
            // NB When we dispose ReservedMemory, we could already lose an explicit
            // reference to its OwnedMemory, so the last release of ReservedMemory
            // should return the backing array to the pool.

            if (newReferenceCount == 0) {
                Dispose();
            }
            base.OnReferenceCountChanged(newReferenceCount);
        }

        public static OwnedMemory<T> Create(T[] array) {
            object pooled;
            if (Pool.TryDequeue(out pooled)) {
                var asOwnedPooledArray = (OwnedPooledArray<T>)pooled;
                // ReSharper disable once PossibleNullReferenceException
                asOwnedPooledArray.Initialize(array, 0, array.Length);
                return asOwnedPooledArray;
            }
            return new OwnedPooledArray<T>(array);
        }
    }
}