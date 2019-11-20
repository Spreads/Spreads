// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;

namespace Spreads.Serialization
{
    /// <summary>
    /// Serialize a generic object T to a pointer, prefixed with version and length.
    /// </summary>
    public abstract class BinarySerializer<T>
    {
        /// <summary>
        /// An type id that must be unique in user application.
        /// Could be stored separately from payload and checked during deserialization.
        /// Not used at the moment.
        /// </summary>
        public virtual byte KnownTypeId { get; } = 0;

        public abstract short FixedSize { get; }

        /// <summary>
        /// Returns the size of serialized value payload.
        /// When <paramref name="payload"/> parameter is not null this method should additionally
        /// serialize the value into <paramref name="payload"/> <see cref="BufferWriter"/>.
        /// </summary>
        ///
        /// <remarks>
        ///
        /// For variable size types, performing actual serialization to a temp buffer is often the only
        /// way to calculate the payload size. Write to <paramref name="payload"/> directly in that case
        /// and do not use a separate buffer. The <paramref name="payload"/> buffer content will then be used
        /// by <see cref="BinarySerializer"/> Write methods instead of performing serialization the second time.
        ///
        /// <para />
        ///
        /// Do not include length prefix in payload because <see cref="BinarySerializer"/> does so
        /// automatically and guarantees than <see cref="Read"/> method will receive the source
        /// buffer of exact payload length.
        ///
        /// </remarks>
        /// <param name="value">Value to serialize.</param>
        /// <param name="payload">If not <see cref="FixedSize"/> then this parameter is not null
        /// and serializer must write actual payload into it.</param>
        /// <returns></returns>
        public abstract int SizeOf(in T value, BufferWriter payload);

        /// <summary>
        /// Serializes a value to the <paramref name="destination"/> buffer.
        /// This method is called by <see cref="BinarySerializer"/> only when <see cref="SizeOf"/> returned
        /// positive length with default/empty temporaryBuffer.
        /// </summary>
        /// <param name="value">A value to serialize.</param>
        /// <param name="destination">A buffer to write to. Has the length returned from <see cref="SizeOf"/></param>
        public abstract int Write(in T value, DirectBuffer destination);

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from <paramref name="source"/>,
        /// returns the number of bytes consumed.
        /// </summary>
        /// <param name="source">A buffer to read from.</param>
        /// <param name="value">Deserialized value.</param>
        /// <returns>Number of bytes consumed. Must be equal to <paramref name="source"/> buffer length on success.
        /// Any other value is assumed a failure.</returns>
        public abstract int Read(DirectBuffer source, out T value);
    }

    internal abstract class InternalSerializer<T> : BinarySerializer<T>
    {
    }
}
