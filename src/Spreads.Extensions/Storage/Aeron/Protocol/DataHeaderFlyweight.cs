using System;
using System.Runtime.InteropServices;
using System.Text;
using Spreads.Serialization;

namespace Spreads.Storage.Aeron.Protocol {

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = DataHeaderFlyweight.HEADER_LENGTH)]
    public struct DataHeader {
        public Header Header;
        public int TermOffset;
        public int SessionID;
        public int StreamID;
        public int TermID;
        public long ReservedValue;
    }

    /**
 * HeaderFlyweight for Data Header
 *
 * <a href="https://github.com/real-logic/Aeron/wiki/Protocol-Specification#data-frame">Data Frame</a>
 */
    public class DataHeaderFlyweight {
        private readonly DirectBuffer _buffer;
        public HeaderFlyweight HeaderFlyweight;
        /**
         * Length of the Data Header
         */
        public const int HEADER_LENGTH = 32;

        /**
         * Begin Flag
         */
        public const short BEGIN_FLAG = 0x80;

        /**
         * End Flag
         */
        public const short END_FLAG = 0x40;

        /**
         * Begin and End Flags
         */
        public const short BEGIN_AND_END_FLAGS = BEGIN_FLAG | END_FLAG;

        public const long DEFAULT_RESERVE_VALUE = 0L;

        public const int TERM_OFFSET_FIELD_OFFSET = 8;
        public const int SESSION_ID_FIELD_OFFSET = 12;
        public const int STREAM_ID_FIELD_OFFSET = 16;
        public const int TERM_ID_FIELD_OFFSET = 20;
        public const int RESERVED_VALUE_OFFSET = 24;
        public const int DATA_OFFSET = HEADER_LENGTH;

        public DataHeaderFlyweight() {
        }

        public DataHeaderFlyweight(DirectBuffer buffer) {
            _buffer = buffer;
            HeaderFlyweight = new HeaderFlyweight(_buffer);
        }


        public int SessionId
        {
            get { return _buffer.ReadInt32(SESSION_ID_FIELD_OFFSET); }
            set { _buffer.WriteInt32(SESSION_ID_FIELD_OFFSET, value); }
        }

        public int StreamId
        {
            get { return _buffer.ReadInt32(STREAM_ID_FIELD_OFFSET); }
            set { _buffer.WriteInt32(STREAM_ID_FIELD_OFFSET, value); }
        }

        public int TermId
        {
            get { return _buffer.ReadInt32(TERM_ID_FIELD_OFFSET); }
            set { _buffer.WriteInt32(TERM_ID_FIELD_OFFSET, value); }
        }

        public int TermOffset
        {
            get { return _buffer.ReadInt32(TERM_OFFSET_FIELD_OFFSET); }
            set { _buffer.WriteInt32(TERM_OFFSET_FIELD_OFFSET, value); }
        }


        public long ReservedValue
        {
            get { return _buffer.ReadInt64(RESERVED_VALUE_OFFSET); }
            set { _buffer.WriteInt64(RESERVED_VALUE_OFFSET, value); }
        }

        public int DataOffset => DATA_OFFSET;

        public DataHeader DataHeader
        {
            get { return _buffer.Read<DataHeader>(0); }
            set { _buffer.Write(0, value); }
        }

        public Header Header
        {
            get { return _buffer.Read<Header>(0); }
            set { _buffer.Write(0, value); }
        }

        /**
         * Return an initialised default Data Frame Header.
         *
         * @param sessionId for the header
         * @param streamId  for the header
         * @param termId    for the header
         * @return byte array containing the header
         */
        public static DataHeader CreateDefaultHeader(int sessionId, int streamId, int termId) {
            return new DataHeader {
                Header =
                {
                    Version = HeaderFlyweight.CURRENT_VERSION,
                    Flags = (byte) BEGIN_AND_END_FLAGS,
                    Type = (short) HeaderFlyweight.HDR_TYPE_DATA
                },
                SessionID = sessionId,
                StreamID = streamId,
                TermID = termId,
                ReservedValue = DEFAULT_RESERVE_VALUE
            };
        }

        public override string ToString() {
            var sb = new StringBuilder();
            var formattedFlags = Convert.ToString(Header.Flags, 2).PadLeft(8, '0');

            sb.Append("Data Header{")
                .Append("frame_length=").Append(Header.FrameLength)
                .Append(" version=").Append(Header.Version)
                .Append(" flags=").Append(formattedFlags)
                .Append(" type=").Append(Header.Type)
                .Append(" term_offset=").Append(TermOffset)
                .Append(" session_id=").Append(SessionId)
                .Append(" stream_id=").Append(StreamId)
                .Append(" term_id=").Append(TermId)
                .Append(" reserved_value=").Append(ReservedValue)
                .Append("}");

            return sb.ToString();
        }
    }

}
