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
        /// <param name="rentedBuffer">A buffer that was previously rented from the shared ArrayPool</param>
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