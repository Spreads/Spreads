// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Buffers;
using System.IO;

namespace Spreads.Serialization
{
    /// <summary>
    /// Wraps a rented buffer and returns it to the shared pool on Dispose
    /// </summary>
    internal class RentedBufferStream : MemoryStream
    {
        private readonly OwnedBuffer<byte> _rentedBuffer;

        /// <summary>
        /// Wraps a rented buffer and returns it to the shared pool on Dispose
        /// </summary>
        /// <param name="rentedBuffer">A buffer that was previously rented from the shared BufferPool</param>
        /// <param name="count"></param>
        public RentedBufferStream(OwnedBuffer<byte> rentedBuffer, int count) : base(GetSegment(rentedBuffer).Array, 0, count)
        {
            _rentedBuffer = rentedBuffer;
        }

        private static ArraySegment<byte> GetSegment(OwnedBuffer<byte> rentedBuffer)
        {
            if (rentedBuffer.Buffer.TryGetArray(out var segment))
            {
                return segment;
            }
            else
            {
                throw new ApplicationException();
            }
        }

        protected override void Dispose(bool disposing)
        {
            _rentedBuffer.Release();
            //_rentedBuffer.Dispose();
            base.Dispose(disposing);
        }

        ~RentedBufferStream()
        {
            Dispose(false);
        }
    }
}