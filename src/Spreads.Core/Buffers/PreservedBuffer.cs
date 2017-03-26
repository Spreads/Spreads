// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Buffers
{
    // NB this pattern will soon be added to CoreFxLab upstream, but for now
    // imitate the API with *Pr*eserved instead of *R*eserved names.
    // We achieve safe disposal by always passing ownership of a buffer segment
    // and never having two places working with the same segment.

    public struct PreservedBuffer<T> : IDisposable
    {
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
    }
}