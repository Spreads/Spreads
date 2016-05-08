using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
    public class Header {
        private readonly int _positionBitsToShift;
        private int _initialTermId;
        private int _offset = 0;
        private DirectBuffer _buffer;

        /**
         * Construct a header that references a buffer for the log.
         *
         * @param initialTermId       this stream started at.
         * @param positionBitsToShift for calculating positions.
         */
        public Header(int initialTermId, int positionBitsToShift) {
            _initialTermId = initialTermId;
            _positionBitsToShift = positionBitsToShift;
        }

        /**
         * Get the current position to which the image has advanced on reading this message.
         *
         * @return the current position to which the image has advanced on reading this message.
         */
        public long Position
        {
            get
            {
                var resultingOffset = BitUtil.Align(TermOffset + FrameLength, FrameDescriptor.FRAME_ALIGNMENT);
                return LogBufferDescriptor.ComputePosition(TermId, resultingOffset, _positionBitsToShift, _initialTermId);
            }
        }

        public int InitialTermId
        {
            get { return _initialTermId; }
            set { _initialTermId = value; }
        }
        public int Offset
        {
            get { return _offset; }
            set { _offset = value; }
        }

        public DirectBuffer Buffer
        {
            get { return _buffer; }
            set { _buffer = value; }
        }


        /**
         * The total length of the frame including the header.
         *
         * @return the total length of the frame including the header.
         */
        public int FrameLength => _buffer.ReadInt32(_offset);

        /**
         * The session ID to which the frame belongs.
         *
         * @return the session ID to which the frame belongs.
         */
        public int SessionId => _buffer.ReadInt32(_offset + DataHeaderFlyweight.SESSION_ID_FIELD_OFFSET);

        /**
         * The stream ID to which the frame belongs.
         *
         * @return the stream ID to which the frame belongs.
         */
        public int StreamId => _buffer.ReadInt32(_offset + DataHeaderFlyweight.STREAM_ID_FIELD_OFFSET);

        /**
         * The term ID to which the frame belongs.
         *
         * @return the term ID to which the frame belongs.
         */
        public int TermId => _buffer.ReadInt32(_offset + DataHeaderFlyweight.TERM_ID_FIELD_OFFSET);

        /**
         * The offset in the term at which the frame begins. This will be the same as {@link #offset()}
         *
         * @return the offset in the term at which the frame begins.
         */
        public int TermOffset => _offset;

        /**
         * The type of the the frame which should always be {@link DataHeaderFlyweight#HDR_TYPE_DATA}
         *
         * @return type of the the frame which should always be {@link DataHeaderFlyweight#HDR_TYPE_DATA}
         */
        public int Type => _buffer.ReadInt32(_offset + HeaderFlyweight.TYPE_FIELD_OFFSET) & 0xFFFF;

        /**
         * The flags for this frame. Valid flags are {@link DataHeaderFlyweight#BEGIN_FLAG}
         * and {@link DataHeaderFlyweight#END_FLAG}. A convenience flag {@link DataHeaderFlyweight#BEGIN_AND_END_FLAGS}
         * can be used for both flags.
         *
         * @return the flags for this frame.
         */
        public byte Flags => _buffer.ReadByte(_offset + HeaderFlyweight.FLAGS_FIELD_OFFSET);

        /**
         * Get the value stored in the reserve space at the end of a data frame header.
         * <p>
         * Note: The value is in {@link ByteOrder#LITTLE_ENDIAN} format.
         *
         * @return the value stored in the reserve space at the end of a data frame header.
         * @see DataHeaderFlyweight
         */
        public long RreservedValue => _buffer.ReadInt64(_offset + DataHeaderFlyweight.RESERVED_VALUE_OFFSET);
    }

}
