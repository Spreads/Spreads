using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage.Logbuffer {
    /**
 * Description of the structure for message framing in a log buffer.
 *
 * All messages are logged in frames that have a minimum header layout as follows plus a reserve then
 * the encoded message follows:
 *
 * <pre>
 *   0                   1                   2                   3
 *   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
 *  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
 *  |R|                       Frame Length                          |
 *  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-------------------------------+
 *  |  Version      |B|E| Flags     |             Type              |
 *  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-------------------------------+
 *  |R|                       Term Offset                           |
 *  +-+-------------------------------------------------------------+
 *  |                      Additional Fields                       ...
 * ...                                                              |
 *  +---------------------------------------------------------------+
 *  |                        Encoded Message                       ...
 * ...                                                              |
 *  +---------------------------------------------------------------+
 * </pre>
 *
 * The (B)egin and (E)nd flags are used for message fragmentation. R is for reserved bit.
 * Both are set for a message that does not span frames.
 */
    public class FrameDescriptor {
        /**
         * Alignment as a multiple of bytes for each frame. The length field will store the unaligned length in bytes.
         */
        public const int FRAME_ALIGNMENT = 32;

        /**
         * Beginning fragment of a frame.
         */
        public const byte BEGIN_FRAG_FLAG = (byte)(1 << 7);

        /**
         * End fragment of a frame.
         */
        public const byte END_FRAG_FLAG = (byte)(1 << 6);

        /**
         * End fragment of a frame.
         */
        public const byte UNFRAGMENTED = BEGIN_FRAG_FLAG | END_FRAG_FLAG;

        /**
         * Offset within a frame at which the version field begins
         */
        public const int VERSION_OFFSET = DataHeaderFlyweight.VERSION_FIELD_OFFSET;

        /**
         * Offset within a frame at which the flags field begins
         */
        public const int FLAGS_OFFSET = DataHeaderFlyweight.FLAGS_FIELD_OFFSET;

        /**
         * Offset within a frame at which the type field begins
         */
        public const int TYPE_OFFSET = DataHeaderFlyweight.TYPE_FIELD_OFFSET;

        /**
         * Offset within a frame at which the term offset field begins
         */
        public const int TERM_OFFSET = DataHeaderFlyweight.TERM_OFFSET_FIELD_OFFSET;

        /**
         * Offset within a frame at which the term id field begins
         */
        public const int TERM_ID_OFFSET = DataHeaderFlyweight.TERM_ID_FIELD_OFFSET;

        /**
         * Padding frame type to indicate the message should be ignored.
         */
        public const int PADDING_FRAME_TYPE = HeaderFlyweight.HDR_TYPE_PAD;

        /**
         * Compute the maximum supported message length for a buffer of given capacity.
         *
         * @param capacity of the log buffer.
         * @return the maximum supported length for a message.
         */
        public static int computeMaxMessageLength(final int capacity) {
            return capacity / 8;
        }

        /**
         * The buffer offset at which the length field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the length field begins.
         */
        public static int lengthOffset(final int termOffset) {
            return termOffset;
        }

        /**
         * The buffer offset at which the version field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the version field begins.
         */
        public static int versionOffset(final int termOffset) {
            return termOffset + VERSION_OFFSET;
        }

        /**
         * The buffer offset at which the flags field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the flags field begins.
         */
        public static int flagsOffset(final int termOffset) {
            return termOffset + FLAGS_OFFSET;
        }

        /**
         * The buffer offset at which the type field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the type field begins.
         */
        public static int typeOffset(final int termOffset) {
            return termOffset + TYPE_OFFSET;
        }

        /**
         * The buffer offset at which the term offset field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the term offset field begins.
         */
        public static int termOffsetOffset(final int termOffset) {
            return termOffset + TERM_OFFSET;
        }

        /**
         * The buffer offset at which the term id field begins.
         *
         * @param termOffset at which the frame begins.
         * @return the offset at which the term id field begins.
         */
        public static int termIdOffset(final int termOffset) {
            return termOffset + TERM_ID_OFFSET;
        }

        /**
         * Read the type of of the frame from header.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @return the value of the frame type header.
         */
        public static int frameVersion(final UnsafeBuffer buffer, final int termOffset) {
            return buffer.getByte(versionOffset(termOffset));
        }

        /**
         * Read the type of of the frame from header.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @return the value of the frame type header.
         */
        public static int frameType(final UnsafeBuffer buffer, final int termOffset) {
            return buffer.getShort(typeOffset(termOffset), LITTLE_ENDIAN) & 0xFFFF;
        }

        /**
         * Is the frame starting at the termOffset a padding frame at the end of a buffer?
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @return true if the frame is a padding frame otherwise false.
         */
        public static boolean isPaddingFrame(final UnsafeBuffer buffer, final int termOffset) {
            return buffer.getShort(typeOffset(termOffset)) == PADDING_FRAME_TYPE;
        }

        /**
         * Get the length of a frame from the header.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @return the value for the frame length.
         */
        public static int frameLength(final UnsafeBuffer buffer, final int termOffset) {
            return buffer.getInt(termOffset, LITTLE_ENDIAN);
        }

        /**
         * Get the length of a frame from the header as a volatile read.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @return the value for the frame length.
         */
        public static int frameLengthVolatile(final UnsafeBuffer buffer, final int termOffset) {
            int frameLength = buffer.getIntVolatile(termOffset);

            if (ByteOrder.nativeOrder() != LITTLE_ENDIAN) {
                frameLength = Integer.reverseBytes(frameLength);
            }

            return frameLength;
        }

        /**
         * Write the length header for a frame in a memory ordered fashion.
         *
         * @param buffer      containing the frame.
         * @param termOffset  at which a frame begins.
         * @param frameLength field to be set for the frame.
         */
        public static void frameLengthOrdered(final UnsafeBuffer buffer, final int termOffset, int frameLength) {
            if (ByteOrder.nativeOrder() != LITTLE_ENDIAN) {
                frameLength = Integer.reverseBytes(frameLength);
            }

            buffer.putIntOrdered(termOffset, frameLength);
        }

        /**
         * Write the type field for a frame.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @param type       type value for the frame.
         */
        public static void frameType(final UnsafeBuffer buffer, final int termOffset, final int type) {
            buffer.putShort(typeOffset(termOffset), (short)type, LITTLE_ENDIAN);
        }

        /**
         * Write the flags field for a frame.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @param flags      value for the frame.
         */
        public static void frameFlags(final UnsafeBuffer buffer, final int termOffset, final byte flags) {
            buffer.putByte(flagsOffset(termOffset), flags);
        }

        /**
         * Write the term offset field for a frame.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         */
        public static void frameTermOffset(final UnsafeBuffer buffer, final int termOffset) {
            buffer.putInt(termOffsetOffset(termOffset), termOffset, LITTLE_ENDIAN);
        }

        /**
         * Write the term id field for a frame.
         *
         * @param buffer     containing the frame.
         * @param termOffset at which a frame begins.
         * @param termId     value for the frame.
         */
        public static void frameTermId(final UnsafeBuffer buffer, final int termOffset, final int termId) {
            buffer.putInt(termIdOffset(termOffset), termId, LITTLE_ENDIAN);
        }
    }
}
