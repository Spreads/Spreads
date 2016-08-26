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

using System;
using System.IO;
using Spreads.Buffers;

namespace Spreads.Serialization {


    public enum BinaryConverterErrorCode : int {
        NotEnoughCapacity = -1
    }

    /// <summary>
    /// Convert a generic object T to a pointer prefixed with version and length.
    /// 
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |R|              Length (including this header)                 |
    /// +---------------------------------------------------------------+
    /// |  Ver  | Flg |C|    TypeEnum   |  TypeSize     | SubTypeEnum   |
    /// +---------------------------------------------------------------+
    /// |                     Serialized Payload                      ...
    /// </summary>
    public interface IBinaryConverter<T> {
        /// <summary>
        /// Equivalent to check Size > 0
        /// </summary>
        bool IsFixedSize { get; }

        /// <summary>
        /// Zero for variable-length types, positive value for fixed-size types.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Version of the converter.
        /// </summary>
        byte Version { get; }

        /// <summary>
        /// Returns the size of serialized bytes including the version+lenght header.
        /// For types with non-fixed size this method could serialize value into the temporaryStream if it is not 
        /// possible to calculate serialized bytes length without actually performing serialization.
        /// The stream temporaryStream contains a header and its length is equal to the returned value.
        /// </summary>
        int SizeOf(T value, out MemoryStream temporaryStream);

        /// <summary>
        /// Write serialized value to the buffer at offset if there is enough capacity
        /// </summary>
        int Write(T value, ref DirectBuffer destination, uint offset = 0u, MemoryStream temporaryStream = null);

        /// <summary>
        /// Reads new value or fill existing value with data from the pointer, 
        /// returns number of bytes read including any header.
        /// If not IsFixedSize, checks that version from the pointer equals the Version property.
        /// </summary>
        int Read(IntPtr ptr, ref T value);
    }
}
