// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;

namespace Spreads.Buffers
{

    /// <summary>
    /// A struct that wraps a System.Memory.Buffer and its DisposableReservation that is returned after calling buffer.Reserver().
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying OwnedPooledArray
    /// will never be returned to the pool and memory will leak.
    /// Use Clone() method to create a copy of this buffer and ensure that the underlying OwnedPooledArray is not returned to the pool.
    /// When adding to a Spreads disposable collection (e.g. SortedMap) ownership is transfered to a collection and PreservedBuffer
    /// will be disposed during disposal of that collection. To keep ownership outside the collection, use PreservedBuffer.Clone() method and 
    /// add a cloned PreservedBuffer value to the collection.
    /// </summary>
    public struct PreservedBuffer<T> : IDisposable
    {
        public static bool TrackLeaks { get; set; }

        private DisposableReservation<T> _reservation;

        public PreservedBuffer(Buffer<T> buffer)
        {
            Buffer = buffer;
            _reservation = buffer.Reserve();
        }

        public Buffer<T> Buffer { get; }

        public void Dispose()
        {
            _reservation.Dispose();
        }

        public PreservedBuffer<T> Clone()
        {
            return new PreservedBuffer<T>(Buffer);
        }
    }
}