// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Diagnostics;

namespace Spreads.Buffers
{
    // NB this pattern will soon be added to CoreFxLab upstream, but for now
    // imitate the API with *Pr*eserved instead of *R*eserved names.
    // We achieve safe disposal by always passing ownership of a buffer segment
    // and never having two places working with the same segment.

    /// <summary>
    /// A struct that wraps a System.Memory.Buffer and its DisposableReservation that is returned after calling buffer.Reserver().
    /// Increases the ref count of underlying OwnedBuffer by one.
    /// Use this struct carefully: it must always be explicitly disposed, otherwise underlying OwnedPooledArray
    /// will never be returned to a pool and memory will leak.
    /// Use Clone() method to create a copy of this buffer and ensure that the underlying OwnedPooledArray is not returned to the pool.
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