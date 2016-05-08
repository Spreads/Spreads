using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
    /// <summary>
    /// Utility for applying a header to a message in a term buffer.
    /// 
    /// This class is designed to be thread safe to be used across multiple producers and makes the header
    /// visible in the correct order for consumers.
    /// </summary>

    public struct HeaderWriter {
        private readonly long versionFlagsType;
        private readonly long sessionId;
        private readonly long streamId;

        public HeaderWriter(DirectBuffer defaultHeader) {
            versionFlagsType = ((long)defaultHeader.ReadInt32(HeaderFlyweight.VERSION_FIELD_OFFSET)) << 32;
            sessionId = ((long)defaultHeader.ReadInt32(DataHeaderFlyweight.SESSION_ID_FIELD_OFFSET)) << 32;
            streamId = defaultHeader.ReadInt32(DataHeaderFlyweight.STREAM_ID_FIELD_OFFSET) & 0xFFFFFFFFL;
        }

        /**
         * Write a header to the term buffer in {@link ByteOrder#LITTLE_ENDIAN} format using the minimum instructions.
         *
         * @param termBuffer to be written to.
         * @param offset     at which the header should be written.
         * @param length     of the fragment including the header.
         * @param termId     of the current term buffer.
         */
        public void Write(DirectBuffer termBuffer, int offset, int length, int termId) {
            var lengthVersionFlagsType = versionFlagsType | ((-length) & 0xFFFFFFFFL);
            var termOffsetSessionId = sessionId | offset;
            var streamAndTermIds = streamId | (((long)termId) << 32);

            termBuffer.VolatileWriteInt64(offset + HeaderFlyweight.FRAME_LENGTH_FIELD_OFFSET, lengthVersionFlagsType);
            // NB compare .NET Volatile.Write and Java storeFence docs:
            // .NET: if a read or write appears before this method in the code, the processor cannot move it after this method.
            // Ensures that loads and stores before the fence will not be reordered with stores after the fence;
            // UnsafeAccess.UNSAFE.storeFence();

            termBuffer.WriteInt64(offset + DataHeaderFlyweight.TERM_OFFSET_FIELD_OFFSET, termOffsetSessionId);
            termBuffer.WriteInt64(offset + DataHeaderFlyweight.STREAM_ID_FIELD_OFFSET, streamAndTermIds);
        }
    }

}
