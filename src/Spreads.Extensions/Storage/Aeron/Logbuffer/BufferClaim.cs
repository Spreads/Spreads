using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {

    /// <summary>
    /// Represents a claimed range in a buffer to be used for recording a message without copy semantics for later commit.
    /// <para>
    /// The claimed space is in <seealso cref="Buffer"/> between <seealso cref="Offset"/> and <seealso cref="Offset"/> + <seealso cref="Length"/>.
    /// When the buffer is filled with message data, use <seealso cref="Commit()"/> to make it available to subscribers.
    /// </para>
    /// <para>
    /// If the claimed space is no longer required it can be aborted by calling <seealso cref="Abort()"/>.
    /// </para>
    /// </summary>
    public struct BufferClaim {
        private readonly DirectBuffer _buffer;

        /**
         * Wrap a region of an underlying log buffer so can can represent a claimed space for use by a publisher.
         *
         * @param buffer to be wrapped.
         * @param offset at which the claimed region begins including space for the header.
         * @param length length of the underlying claimed region including space for the header.
         */
        public BufferClaim(DirectBuffer buffer, int offset, int length) {
            _buffer = new DirectBuffer(length, buffer.Data + offset);
        }

        /**
         * The referenced buffer to be used.
         *
         * @return the referenced buffer to be used..
         */
        public DirectBuffer Buffer => _buffer;

        /**
         * The offset in the buffer at which the claimed range begins.
         *
         * @return offset in the buffer at which the range begins.
         */
        public int Offset => HeaderFlyweight.HEADER_LENGTH;


        public int Length
        {
            get { checked { return (int)_buffer.Length - HeaderFlyweight.HEADER_LENGTH; } }
        }

        public long ReservedValue
        {
            get { return _buffer.ReadInt64(DataHeaderFlyweight.RESERVED_VALUE_OFFSET); }
            set { _buffer.WriteInt64(DataHeaderFlyweight.RESERVED_VALUE_OFFSET, value); }
        }

        /// <summary>
        /// Commit the message to the log buffer so that is it available to subscribers.
        /// </summary>
        public void Commit() {
            checked {
                var frameLength = (int)_buffer.Length;
                _buffer.VolatileWriteInt32(HeaderFlyweight.FRAME_LENGTH_FIELD_OFFSET, frameLength);
            }
        }


        /// <summary>
        /// Abort a claim of the message space to the log buffer so that the log can progress by ignoring this claim.
        /// </summary>
        public void Abort() {
            checked {
                var frameLength = (int)_buffer.Length;
                _buffer.WriteInt16(HeaderFlyweight.TYPE_FIELD_OFFSET, (short)HeaderFlyweight.HDR_TYPE_PAD);
                _buffer.VolatileWriteInt32(HeaderFlyweight.FRAME_LENGTH_FIELD_OFFSET, frameLength);
            }

        }
    }
}
