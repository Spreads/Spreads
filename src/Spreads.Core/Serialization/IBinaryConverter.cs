// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using Spreads.DataTypes;

namespace Spreads.Serialization
{
    public enum BinaryConverterErrorCode
    {
        NotEnoughCapacity = -1
    }

    /// <summary>
    /// Serialize a generic object T to a pointer, prefixed with version and length.
    /// </summary>
    /// <remarks>
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Ver  |T|D|C|B|    TypeEnum   |    TypeSize   | SubTypeEnum   |
    /// +---------------------------------------------------------------+
    /// |R|     Payload Length (if VarLen/TypeSize is negative )        |
    /// +---------------------------------------------------------------+
    /// |                     Serialized Payload                      ...
    /// C - compressed
    /// D - diffed (if a type implements <see cref="IDelta{T}"/>)
    /// B - binary format. If not set then the payload is JSON,
    /// if set then payload is custom binary (payload could have
    /// it's own headers e.g. Blosc)
    /// T - value has Timestamp as the first element of payload for binary case or Timestamp field on JSON object.
    /// </remarks>
    public interface IBinaryConverter<T>
    {
        /// <summary>
        /// Equivalent to check Size > 0
        /// </summary>
        bool IsFixedSize { get; }

        // TODO -1 for variable-length types, zero is a valid length
        /// <summary>
        /// Positive value for fixed-size types, -1 for variable-length types.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Version of the converter. 15 (4 bits) max.
        /// </summary>
        byte ConverterVersion { get; }

        /// <summary>
        /// Returns the size of serialized bytes including the version+lenght header.
        /// For types with non-fixed size this method could serialize value into a temporary stream if it is not
        /// possible to calculate serialized bytes length without actually performing serialization.
        /// The stream temporaryStream contains a header and its length is equal to the returned value.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="temporaryStream">A stream a value is serialized into if it is not possible to calculate serialized buffer size
        /// without actually performing serialization.</param>
        /// <param name="format">Preferred serialization format.</param>
        /// <param name="timestamp">Optional Timestamp to save with data.</param>
        /// <returns></returns>
        int SizeOf(T value, out MemoryStream temporaryStream, 
            SerializationFormat format = SerializationFormat.Binary, Timestamp timestamp = default);

        /// <summary>
        /// Write serialized value to the buffer at offset if there is enough capacity.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="pinnedDestination">A pinned pointer to a buffer to serialize the value into. It must have at least number of bytes returned by SizeOf().</param>
        /// <param name="temporaryStream">A stream that was returned by SizeOf method. If it is not null then its content is written to the buffer.</param>
        /// <param name="format">Compression method.</param>
        /// <param name="timestamp">Optional Timestamp to save with data.</param>
        /// <returns>Returns the number of bytes written to the destination buffer or a negative error code that corresponds to <see cref="BinaryConverterErrorCode"/>.</returns>
        int Write(T value, IntPtr pinnedDestination, MemoryStream temporaryStream = null, 
            SerializationFormat format = SerializationFormat.Binary, Timestamp timestamp = default);

        /// <summary>
        /// Reads new value or fill existing value with data from the pointer,
        /// returns number of bytes read including any header.
        /// If not IsFixedSize, checks that version from the pointer equals the Version property.
        /// </summary>
        int Read(IntPtr ptr, out T value, out Timestamp timestamp);

    }
}