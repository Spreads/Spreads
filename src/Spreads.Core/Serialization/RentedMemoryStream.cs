// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System.IO;
using Spreads.Buffers;
using System.Buffers;

namespace Spreads.Serialization
{
    /// <summary>
    /// Wraps a rented buffer and returns it to the shared pool on Dispose
    /// </summary>
    internal class RentedMemoryStream : MemoryStream {
        private readonly byte[] _rentedBuffer;

        /// <summary>
        /// Wraps a rented buffer and returns it to the shared pool on Dispose
        /// </summary>
        /// <param name="rentedBuffer">A buffer that was previously rented from the shared BufferPool</param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public RentedMemoryStream(byte[] rentedBuffer, int offset, int count) : base(rentedBuffer, offset, count) {
            _rentedBuffer = rentedBuffer;
        }

        protected override void Dispose(bool disposing) {
            ArrayPool<byte>.Shared.Return(_rentedBuffer);
            base.Dispose(disposing);
        }
    }
}