// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;

namespace Spreads.Serialization
{
    /// <summary>
    /// Serialize a generic object T to a pointer, prefixed with version and length.
    /// </summary>
    public interface IBinarySerializer<T>
    {
        /// <summary>
        /// Version of the serializer. Must be from 1 to 3. Subsequent incompatible changes in binary layout will require a new type <typeparamref name="T"/>.
        /// </summary>
        byte SerializerVersion { get; }

        /// <summary>
        /// A unique type id value from 1 to 127 that is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// </summary>
        byte KnownTypeId { get; }

        short FixedSize { get; }

        /// <summary>
        /// Returns the size of serialized value payload.
        /// When serialized payload length is only known after serialization, which is often the case for non-fixed size type,
        /// this method must serialize the value into <paramref name="temporaryBuffer"/>.
        /// The <paramref name="temporaryBuffer"/> <see cref="RetainedMemory{T}"/> could be taken from
        /// <see cref="BufferPool.Retain"/>. The buffer is owned by the caller, no other references to it should remain after the call.
        /// When non-empty <paramref name="temporaryBuffer"/> is returned the <see cref="Write"/> method is ignored
        /// and the buffer is written completely by <see cref="Serialization.BinarySerializer"/> write method, which then disposes
        /// the buffer.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="temporaryBuffer">A buffer with serialized payload. (optional, for cases when the serialized size is not known without performing serialization)</param>
        int SizeOf(in T value, out RetainedMemory<byte> temporaryBuffer);

        /// <summary>
        /// Serializes a value to the <paramref name="destination"/> buffer.
        /// This method is called by <see cref="Serialization.BinarySerializer"/> only when <see cref="SizeOf"/> returned
        /// positive length with default/empty temporaryBuffer.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="destination">A buffer to write to. Has the length returned from <see cref="SizeOf"/></param>
        int Write(in T value, DirectBuffer destination);

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from <paramref name="source"/>,
        /// returns the number of bytes consumed.
        /// </summary>
        /// <param name="source">A buffer to read from.</param>
        /// <param name="value">Deserialized value.</param>
        /// <returns>Number of bytes consumed. Must be equal to <paramref name="source"/> buffer length on success.
        /// Any other value is assumed a failure.</returns>
        int Read(DirectBuffer source, out T value);
    }
}
