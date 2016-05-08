using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
    /// <summary>
    /// Term buffer appender which supports many producers concurrently writing an append-only log.
    /// 
    /// <b>Note:</b> This class is threadsafe.
    /// 
    /// Messages are appended to a term using a framing protocol as described in <seealso cref="FrameDescriptor"/>.
    /// 
    /// A default message header is applied to each message with the fields filled in for fragment flags, type, term number,
    /// as appropriate.
    /// 
    /// A message of type <seealso cref="FrameDescriptor#PADDING_FRAME_TYPE"/> is appended at the end of the buffer if claimed
    /// space is not sufficiently large to accommodate the message about to be written.
    /// </summary>

    public class TermAppender {
        private readonly DirectBuffer _termBuffer;
        private readonly DirectBuffer _metaDataBuffer;
        /**
         * The append operation tripped the end of the buffer and needs to rotate.
         */
        public const int TRIPPED = -1;

        /**
         * The append operation went past the end of the buffer and failed.
         */
        public const int FAILED = -2;

        /**
         * Construct a view over a term buffer and state buffer for appending frames.
         *
         * @param termBuffer     for where messages are stored.
         * @param metaDataBuffer for where the state of writers is stored manage concurrency.
         */
        public TermAppender(DirectBuffer termBuffer, DirectBuffer metaDataBuffer) {
            this._termBuffer = termBuffer;
            this._metaDataBuffer = metaDataBuffer;
        }

        public DirectBuffer TermBuffer => _termBuffer;

        public DirectBuffer MetaDataBuffer => _metaDataBuffer;


        /**
         * Get the raw value current tail value in a volatile memory ordering fashion.
         *
         * @return the current tail value.
         */

        public long rawTailVolatile() {
            return _metaDataBuffer.VolatileReadInt64(LogBufferDescriptor.TERM_TAIL_COUNTER_OFFSET);
        }

        /**
         * Set the value for the tail counter.
         *
         * @param termId for the tail counter
         */
        public void TailTermId(int termId) {
            _metaDataBuffer.WriteInt64(LogBufferDescriptor.TERM_TAIL_COUNTER_OFFSET, ((long)termId) << 32);
        }

        /**
         * Set the status of the log buffer with StoreStore memory ordering semantics.
         *
         * @param status to be set for the log buffer.
         */
        public void StatusOrdered(int status) {
            _metaDataBuffer.VolatileWriteInt32(LogBufferDescriptor.TERM_STATUS_OFFSET, status);
        }

        /**
         * Claim length of a the term buffer for writing in the message with zero copy semantics.
         *
         * @param header      for writing the default header.
         * @param length      of the message to be written.
         * @param bufferClaim to be updated with the claimed region.
         * @return the resulting offset of the term after the append on success otherwise {@link #TRIPPED} or {@link #FAILED}
         * packed with the termId if a padding record was inserted at the end.
         */
        public long claim(HeaderWriter header, int length, out BufferClaim bufferClaim) {
            int frameLength = length + DataHeaderFlyweight.HEADER_LENGTH;
            int alignedLength = BitUtil.Align(frameLength, FrameDescriptor.FRAME_ALIGNMENT);
            long rawTail = GetAndAddRawTail(alignedLength);
            long termOffset = rawTail & 0xFFFFFFFFL;

            DirectBuffer termBuffer = this.TermBuffer;
            checked {
                int termLength = (int)termBuffer.Length;

                long resultingOffset = termOffset + alignedLength;
                if (resultingOffset > termLength) {
                    resultingOffset = HandleEndOfLogCondition(termBuffer, termOffset, header, termLength,
                        TermId(rawTail));
                    bufferClaim = default(BufferClaim);
                } else {
                    int offset = (int)termOffset;
                    header.Write(termBuffer, offset, frameLength, TermId(rawTail));
                    bufferClaim = new BufferClaim(termBuffer, offset, frameLength);
                }
                return resultingOffset;
            }
        }

        /**
         * Append an unfragmented message to the the term buffer.
         *
         * @param header    for writing the default header.
         * @param srcBuffer containing the message.
         * @param srcOffset at which the message begins.
         * @param length    of the message in the source buffer.
         * @return the resulting offset of the term after the append on success otherwise {@link #TRIPPED} or {@link #FAILED}
         * packed with the termId if a padding record was inserted at the end.
         */
        public long AppendUnfragmentedMessage(
            HeaderWriter header, DirectBuffer srcBuffer, int srcOffset, int length) {
            int frameLength = length + DataHeaderFlyweight.HEADER_LENGTH;
            int alignedLength = BitUtil.Align(frameLength, FrameDescriptor.FRAME_ALIGNMENT);
            long rawTail = GetAndAddRawTail(alignedLength);
            long termOffset = rawTail & 0xFFFFFFFFL;

            DirectBuffer termBuffer = this.TermBuffer;
            checked {
                int termLength = (int)termBuffer.Length;

                long resultingOffset = termOffset + alignedLength;
                if (resultingOffset > termLength) {
                    resultingOffset = HandleEndOfLogCondition(termBuffer, termOffset, header, termLength,
                        TermId(rawTail));
                } else {
                    int offset = (int)termOffset;
                    header.Write(termBuffer, offset, frameLength, TermId(rawTail));
                    termBuffer.WriteBytes(offset + DataHeaderFlyweight.HEADER_LENGTH, srcBuffer, srcOffset, length);
                    FrameDescriptor.FrameLengthOrdered(termBuffer, offset, frameLength);
                }
                return resultingOffset;
            }
        }

        /// <summary>
        /// Append a fragmented message to the the term buffer.
        /// The message will be split up into fragments of MTU length minus header.
        /// </summary>
        /// <param name="header">           for writing the default header. </param>
        /// <param name="srcBuffer">        containing the message. </param>
        /// <param name="srcOffset">        at which the message begins. </param>
        /// <param name="length">           of the message in the source buffer. </param>
        /// <param name="maxPayloadLength"> that the message will be fragmented into. </param>
        /// <returns> the resulting offset of the term after the append on success otherwise <seealso cref="#TRIPPED"/> or <seealso cref="#FAILED"/>
        /// packed with the termId if a padding record was inserted at the end. </returns>

        public long AppendFragmentedMessage(
            HeaderWriter header,
            DirectBuffer srcBuffer,
            int srcOffset,
            int length,
            int maxPayloadLength) {
            int numMaxPayloads = length / maxPayloadLength;
            int remainingPayload = length % maxPayloadLength;
            int lastFrameLength = remainingPayload > 0 ? BitUtil.Align(remainingPayload + DataHeaderFlyweight.HEADER_LENGTH, FrameDescriptor.FRAME_ALIGNMENT) : 0;
            int requiredLength = (numMaxPayloads * (maxPayloadLength + DataHeaderFlyweight.HEADER_LENGTH)) + lastFrameLength;
            throw new NotImplementedException("GetAndAddRawTail returns value before addition");
            long rawTail = GetAndAddRawTail(requiredLength);
            int termId = TermId(rawTail);
            long termOffset = rawTail & 0xFFFFFFFFL;

            DirectBuffer termBuffer = this.TermBuffer;

            int termLength = (int)termBuffer.Length;

            long resultingOffset = termOffset + requiredLength;
            if (resultingOffset > termLength) {
                resultingOffset = HandleEndOfLogCondition(termBuffer, termOffset, header, termLength, termId);
            } else {
                int offset = (int)termOffset;
                byte flags = FrameDescriptor.BEGIN_FRAG_FLAG;
                int remaining = length;
                do {
                    int bytesToWrite = Math.Min(remaining, maxPayloadLength);
                    int frameLength = bytesToWrite + DataHeaderFlyweight.HEADER_LENGTH;
                    int alignedLength = BitUtil.Align(frameLength, FrameDescriptor.FRAME_ALIGNMENT);

                    header.Write(termBuffer, offset, frameLength, termId);
                    termBuffer.WriteBytes(
                        offset + DataHeaderFlyweight.HEADER_LENGTH,
                        srcBuffer,
                        srcOffset + (length - remaining),
                        bytesToWrite);

                    if (remaining <= maxPayloadLength) {
                        flags |= FrameDescriptor.END_FRAG_FLAG;
                    }

                    FrameDescriptor.FrameFlags(termBuffer, offset, flags);
                    FrameDescriptor.FrameLengthOrdered(termBuffer, offset, frameLength);

                    flags = 0;
                    offset += alignedLength;
                    remaining -= bytesToWrite;
                }
                while (remaining > 0);
            }

            return resultingOffset;
        }


        /**
         * Pack the values for termOffset and termId into a long for returning on the stack.
         *
         * @param termId     value to be packed.
         * @param termOffset value to be packed.
         * @return a long with both ints packed into it.
         */
        public static long Pack(int termId, int termOffset) {
            return ((long)termId << 32) | (termOffset & 0xFFFFFFFFL);
        }

        /**
         * The termOffset as a result of the append
         *
         * @param result into which the termOffset value has been packed.
         * @return the termOffset after the append
         */
        public static int TermOffset(long result) {
            return (int)result;
        }

        /**
         * The termId in which the append operation took place.
         *
         * @param result into which the termId value has been packed.
         * @return the termId in which the append operation took place.
         */
        public static int TermId(long result) {
            return (int)(result >> 32);
        }

        private long HandleEndOfLogCondition(
            DirectBuffer termBuffer,
            long termOffset,
            HeaderWriter header,
            int termLength,
            int termId) {
            int resultingOffset = FAILED;

            if (termOffset <= termLength) {
                resultingOffset = TRIPPED;

                if (termOffset < termLength) {
                    int offset = (int)termOffset;
                    int paddingLength = termLength - offset;
                    header.Write(termBuffer, offset, paddingLength, termId);
                    FrameDescriptor.FrameType(termBuffer, offset, FrameDescriptor.PADDING_FRAME_TYPE);
                    FrameDescriptor.FrameLengthOrdered(termBuffer, offset, paddingLength);
                }
            }

            return Pack(termId, resultingOffset);
        }

        private long GetAndAddRawTail(int alignedLength) {
            return _metaDataBuffer.InterlockedAddInt64(LogBufferDescriptor.TERM_TAIL_COUNTER_OFFSET, alignedLength);
        }
    }
}
