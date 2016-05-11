using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;
using static Spreads.Storage.Aeron.BitUtil;

namespace Spreads.Storage.Aeron.Logbuffer {
    /// <summary>
    /// Layout description for log buffers which contains partitions of terms with associated term meta data,
    /// plus ending with overall log meta data.
    /// 
    /// <pre>
    ///  +----------------------------+
    ///  |           Term 0           |
    ///  +----------------------------+
    ///  |           Term 1           |
    ///  +----------------------------+
    ///  |           Term 2           |
    ///  +----------------------------+
    ///  |      Term Meta Data 0      |
    ///  +----------------------------+
    ///  |      Term Meta Data 1      |
    ///  +----------------------------+
    ///  |      Term Meta Data 2      |
    ///  +----------------------------+
    ///  |        Log Meta Data       |
    ///  +----------------------------+
    /// </pre>
    /// </summary>

    public static class LogBufferDescriptor {
        /**
         * The number of partitions the log is divided into with pairs of term and term meta data buffers.
         */
        public const int PARTITION_COUNT = 3;

        /**
         * Minimum buffer length for a log term
         */
        public const int TERM_MIN_LENGTH = 64 * 1024; // TODO: make a sensible default

        // ********************************
        // *** Term Meta Data Constants ***
        // ********************************

        /**
         * A term is currently clean or in use.
         */
        public const int CLEAN = 0;

        /**
         * A term is dirty and requires cleaning.
         */
        public const int NEEDS_CLEANING = 1;

        /**
         * Offset within the term meta data where the tail value is stored.
         */
        public static int TERM_TAIL_COUNTER_OFFSET;

        /**
         * Offset within the term meta data where current status is stored
         */
        public static int TERM_STATUS_OFFSET;

        /**
         * Total length of the term meta data buffer in bytes.
         */
        public static int TERM_META_DATA_LENGTH;

        //static LogBufferDescriptor() {

        //}

        // *******************************
        // *** Log Meta Data Constants ***
        // *******************************

        /**
         * Offset within the log meta data where the active term id is stored.
         */
        public const int LOG_META_DATA_SECTION_INDEX = PARTITION_COUNT * 2;

        /**
         * Offset within the log meta data where the active partition index is stored.
         */
        public static int LOG_ACTIVE_PARTITION_INDEX_OFFSET;

        /**
         * Offset within the log meta data where the time of last SM is stored.
         */
        public static int LOG_TIME_OF_LAST_SM_OFFSET;

        /**
         * Offset within the log meta data where the active term id is stored.
         */
        public static int LOG_INITIAL_TERM_ID_OFFSET;

        /**
         * Offset within the log meta data which the length field for the frame header is stored.
         */
        public static int LOG_DEFAULT_FRAME_HEADER_LENGTH_OFFSET;

        /**
         * Offset within the log meta data which the MTU length is stored;
         */
        public static int LOG_MTU_LENGTH_OFFSET;

        /**
         * Offset within the log meta data which the
         */
        public static int LOG_CORRELATION_ID_OFFSET;

        /**
         * Offset at which the default frame headers begin.
         */
        public static int LOG_DEFAULT_FRAME_HEADER_OFFSET;

        /**
         * Offset at which the default frame headers begin.
         */
        public const int LOG_DEFAULT_FRAME_HEADER_MAX_LENGTH = CACHE_LINE_LENGTH * 2;

        static LogBufferDescriptor() {

            int offset = (CACHE_LINE_LENGTH * 2);
            TERM_TAIL_COUNTER_OFFSET = offset;

            offset += (CACHE_LINE_LENGTH * 2);
            TERM_STATUS_OFFSET = offset;

            offset += (CACHE_LINE_LENGTH * 2);
            TERM_META_DATA_LENGTH = offset;

            /////////////////////////////////////////
            /// 
            offset = 0;
            LOG_ACTIVE_PARTITION_INDEX_OFFSET = offset;

            offset += (CACHE_LINE_LENGTH * 2);
            LOG_TIME_OF_LAST_SM_OFFSET = offset;

            offset += (CACHE_LINE_LENGTH * 2);
            LOG_CORRELATION_ID_OFFSET = offset;
            LOG_INITIAL_TERM_ID_OFFSET = LOG_CORRELATION_ID_OFFSET + SIZE_OF_LONG;
            LOG_DEFAULT_FRAME_HEADER_LENGTH_OFFSET = LOG_INITIAL_TERM_ID_OFFSET + SIZE_OF_INT;
            LOG_MTU_LENGTH_OFFSET = LOG_DEFAULT_FRAME_HEADER_LENGTH_OFFSET + SIZE_OF_INT;

            offset += CACHE_LINE_LENGTH;
            LOG_DEFAULT_FRAME_HEADER_OFFSET = offset;

            LOG_META_DATA_LENGTH = offset + LOG_DEFAULT_FRAME_HEADER_MAX_LENGTH;
        }

        /**
         * Total length of the log meta data buffer in bytes.
         *
         * <pre>
         *   0                   1                   2                   3
         *   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
         *  +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
         *  |                   Active Partition Index                      |
         *  +---------------------------------------------------------------+
         *  |                      Cache Line Padding                      ...
         * ...                                                              |
         *  +---------------------------------------------------------------+
         *  |                 Time of Last Status Message                   |
         *  |                                                               |
         *  +---------------------------------------------------------------+
         *  |                      Cache Line Padding                      ...
         * ...                                                              |
         *  +---------------------------------------------------------------+
         *  |                 Registration / Correlation ID                 |
         *  |                                                               |
         *  +---------------------------------------------------------------+
         *  |                        Initial Term Id                        |
         *  +---------------------------------------------------------------+
         *  |                  Default Frame Header Length                  |
         *  +---------------------------------------------------------------+
         *  |                          MTU Length                           |
         *  +---------------------------------------------------------------+
         *  |                      Cache Line Padding                      ...
         * ...                                                              |
         *  +---------------------------------------------------------------+
         *  |                    Default Frame Header                      ...
         * ...                                                              |
         *  +---------------------------------------------------------------+
         * </pre>
         */
        public static int LOG_META_DATA_LENGTH;

        /// <summary>
        /// Check that term length is valid and alignment is valid.
        /// </summary>
        /// <param name="termLength"> to be checked. </param>
        /// <exception cref="InvalidOperationException"> if the length is not as expected. </exception>
        public static void CheckTermLength(int termLength) {
            if (termLength < TERM_MIN_LENGTH) {
                string s = $"Term length less than min length of {TERM_MIN_LENGTH}, length={termLength}";
                throw new InvalidOperationException(s);
            }

            if ((termLength & (FrameDescriptor.FRAME_ALIGNMENT - 1)) != 0) {
                string s = $"Term length not a multiple of {FrameDescriptor.FRAME_ALIGNMENT}, length={termLength}";
                throw new InvalidOperationException(s);
            }
        }

        /// <summary>
        /// Check that meta data buffer is of sufficient length.
        /// </summary>
        /// <param name="buffer"> to be checked. </param>
        /// <exception cref="InvalidOperationException"> if the buffer is not as expected. </exception>
        public static void CheckMetaDataBuffer(DirectBuffer buffer) {
            checked {
                int capacity = (int)buffer.Length;
                if (capacity < TERM_META_DATA_LENGTH) {
                    string s =
                        $"Meta data buffer capacity less than min length of {TERM_META_DATA_LENGTH:%d}, capacity={capacity:%d}";
                    throw new InvalidOperationException(s);
                }
            }
        }

        /**
         * Get the value of the initial Term id used for this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @return the value of the initial Term id used for this log.
         */
        public static int InitialTermId(DirectBuffer logMetaDataBuffer) {
            return logMetaDataBuffer.ReadInt32(LOG_INITIAL_TERM_ID_OFFSET);
        }

        /**
         * Set the initial term at which this log begins. Initial should be randomised so that stream does not get
         * reused accidentally.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @param initialTermId     value to be set.
         */
        public static void InitialTermId(DirectBuffer logMetaDataBuffer, int initialTermId) {
            logMetaDataBuffer.WriteInt32(LOG_INITIAL_TERM_ID_OFFSET, initialTermId);
        }

        /**
         * Get the value of the MTU length used for this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @return the value of the MTU length used for this log.
         */
        public static int MtuLength(DirectBuffer logMetaDataBuffer) {
            return logMetaDataBuffer.ReadInt32(LOG_MTU_LENGTH_OFFSET);
        }

        /**
         * Set the MTU length used for this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @param mtuLength         value to be set.
         */
        public static void MtuLength(DirectBuffer logMetaDataBuffer, int mtuLength) {
            logMetaDataBuffer.WriteInt32(LOG_MTU_LENGTH_OFFSET, mtuLength);
        }

        /**
         * Get the value of the correlation ID for this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @return the value of the correlation ID used for this log.
         */
        public static long CorrelationId(DirectBuffer logMetaDataBuffer) {
            return logMetaDataBuffer.ReadInt64(LOG_CORRELATION_ID_OFFSET);
        }

        /**
         * Set the correlation ID used for this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @param id                value to be set.
         */
        public static void CorrelationId(DirectBuffer logMetaDataBuffer, long id) {
            logMetaDataBuffer.WriteInt64(LOG_CORRELATION_ID_OFFSET, id);
        }

        /**
         * Get the value of the time of last SM in {@link System#currentTimeMillis()}.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @return the value of time of last SM
         */
        public static long TimeOfLastStatusMessage(DirectBuffer logMetaDataBuffer) {
            return logMetaDataBuffer.VolatileReadInt64(LOG_TIME_OF_LAST_SM_OFFSET);
        }

        /**
         * Set the value of the time of last SM used by the producer of this log.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @param timeInMillis      value of the time of last SM in {@link System#currentTimeMillis()}
         */
        public static void TimeOfLastStatusMessage(DirectBuffer logMetaDataBuffer, long timeInMillis) {
            logMetaDataBuffer.VolatileWriteInt64(LOG_TIME_OF_LAST_SM_OFFSET, timeInMillis);
        }

        /**
         * Get the value of the active partition index used by the producer of this log. Consumers may have a different active
         * index if they are running behind. The read is done with volatile semantics.
         *
         * @param logMetaDataBuffer containing the meta data.
         * @return the value of the active partition index used by the producer of this log.
         */
        public static int ActivePartitionIndex(DirectBuffer logMetaDataBuffer) {
            return logMetaDataBuffer.VolatileReadInt32(LOG_ACTIVE_PARTITION_INDEX_OFFSET);
        }

        /**
         * Set the value of the current active partition index for the producer using memory ordered semantics.
         *
         * @param logMetaDataBuffer    containing the meta data.
         * @param activePartitionIndex value of the active partition index used by the producer of this log.
         */
        public static void ActivePartitionIndex(DirectBuffer logMetaDataBuffer, int activePartitionIndex) {
            logMetaDataBuffer.VolatileWriteInt32(LOG_ACTIVE_PARTITION_INDEX_OFFSET, activePartitionIndex);
        }

        /**
         * Rotate to the next partition in sequence for the term id.
         *
         * @param currentIndex partition index
         * @return the next partition index
         */
        public static int NextPartitionIndex(int currentIndex) {
            return (currentIndex + 1) % PARTITION_COUNT;
        }

        /**
         * Rotate to the previous partition in sequence for the term id.
         *
         * @param currentIndex partition index
         * @return the previous partition index
         */
        public static int PreviousPartitionIndex(int currentIndex) {
            return (currentIndex + (PARTITION_COUNT - 1)) % PARTITION_COUNT;
        }

        /**
         * Determine the partition index to be used given the initial term and active term ids.
         *
         * @param initialTermId at which the log buffer usage began
         * @param activeTermId  that is in current usage
         * @return the index of which buffer should be used
         */
        public static int IndexByTerm(int initialTermId, int activeTermId) {
            return (activeTermId - initialTermId) % PARTITION_COUNT;
        }

        /**
         * Determine the partition index based on number of terms that have passed.
         *
         * @param termCount for the number of terms that have passed.
         * @return the partition index for the term count.
         */
        public static int IndexByTermCount(int termCount) {
            return termCount % PARTITION_COUNT;
        }

        /**
         * Determine the partition index given a stream position.
         *
         * @param position in the stream in bytes.
         * @param positionBitsToShift number of times to right shift the position for term count
         * @return the partition index for the position
         */
        public static int IndexByPosition(long position, int positionBitsToShift) {
            return (int)((position >> positionBitsToShift) % PARTITION_COUNT);
        }

        /**
         * Compute the current position in absolute number of bytes.
         *
         * @param activeTermId        active term id.
         * @param termOffset          in the term.
         * @param positionBitsToShift number of times to left shift the term count
         * @param initialTermId       the initial term id that this stream started on
         * @return the absolute position in bytes
         */
        public static long ComputePosition(
            int activeTermId, int termOffset, int positionBitsToShift, int initialTermId) {
            long termCount = activeTermId - initialTermId; // copes with negative activeTermId on rollover

            return (termCount << positionBitsToShift) + termOffset;
        }

        /**
         * Compute the current position in absolute number of bytes for the beginning of a term.
         *
         * @param activeTermId        active term id.
         * @param positionBitsToShift number of times to left shift the term count
         * @param initialTermId       the initial term id that this stream started on
         * @return the absolute position in bytes
         */
        public static long ComputeTermBeginPosition(
            int activeTermId, int positionBitsToShift, int initialTermId) {
            long termCount = activeTermId - initialTermId; // copes with negative activeTermId on rollover

            return termCount << positionBitsToShift;
        }

        /**
         * Compute the term id from a position.
         *
         * @param position            to calculate from
         * @param positionBitsToShift number of times to right shift the position
         * @param initialTermId       the initial term id that this stream started on
         * @return the term id according to the position
         */
        public static int ComputeTermIdFromPosition(long position, int positionBitsToShift, int initialTermId) {
            return ((int)(position >> positionBitsToShift) + initialTermId);
        }

        /**
         * Compute the term offset from a given position.
         *
         * @param position            to calculate from
         * @param positionBitsToShift number of times to right shift the position
         * @return the offset within the term that represents the position
         */
        public static int ComputeTermOffsetFromPosition(long position, int positionBitsToShift) {
            long mask = (1L << positionBitsToShift) - 1L;

            return (int)(position & mask);
        }

        /**
         * Compute the total length of a log file given the term length.
         *
         * @param termLength on which to base the calculation.
         * @return the total length of the log file.
         */
        public static long ComputeLogLength(int termLength) {
            return
                (termLength * PARTITION_COUNT) +
                (TERM_META_DATA_LENGTH * PARTITION_COUNT) +
                LOG_META_DATA_LENGTH;
        }

        /**
         * Compute the term length based on the total length of the log.
         *
         * @param logLength the total length of the log.
         * @return length of an individual term buffer in the log.
         */
        public static int ComputeTermLength(long logLength) {
            long metaDataSectionLength = (TERM_META_DATA_LENGTH * (long)PARTITION_COUNT) + LOG_META_DATA_LENGTH;

            return (int)((logLength - metaDataSectionLength) / PARTITION_COUNT);
        }

        /**
         * Store the default frame header to the log meta data buffer.
         *
         * @param logMetaDataBuffer into which the default headers should be stored.
         * @param defaultHeader     to be stored.
         * @throws IllegalArgumentException if the default header is larger than {@link #LOG_DEFAULT_FRAME_HEADER_MAX_LENGTH}
         */
        public static void StoreDefaultFrameHeader(DirectBuffer logMetaDataBuffer, DirectBuffer defaultHeader) {
            if (defaultHeader.Length != DataHeaderFlyweight.HEADER_LENGTH) {
                throw new InvalidOperationException(String.Format(
                    "Default header of %d not equal to %d", defaultHeader.Length, DataHeaderFlyweight.HEADER_LENGTH));
            }

            logMetaDataBuffer.WriteInt32(LOG_DEFAULT_FRAME_HEADER_LENGTH_OFFSET, DataHeaderFlyweight.HEADER_LENGTH);
            defaultHeader.Copy(logMetaDataBuffer.Data + LOG_DEFAULT_FRAME_HEADER_OFFSET, 0, DataHeaderFlyweight.HEADER_LENGTH);
            //logMetaDataBuffer.putBytes(LOG_DEFAULT_FRAME_HEADER_OFFSET, defaultHeader, 0, HeaderFlyweight.HEADER_LENGTH);
        }

        /**
         * Get a wrapper around the default frame header from the log meta data.
         *
         * @param logMetaDataBuffer containing the raw bytes for the default frame header.
         * @return a buffer wrapping the raw bytes.
         */
        public static DirectBuffer DefaultFrameHeader(DirectBuffer logMetaDataBuffer) {
            return new DirectBuffer(DataHeaderFlyweight.HEADER_LENGTH, logMetaDataBuffer.Data + LOG_DEFAULT_FRAME_HEADER_OFFSET);
        }

        /**
         * Apply the default header for a message in a term.
         *
         * @param logMetaDataBuffer containing the default headers.
         * @param termBuffer        to which the default header should be applied.
         * @param termOffset        at which the default should be applied.
         */
        public static void ApplyDefaultHeader(
            DirectBuffer logMetaDataBuffer, DirectBuffer termBuffer, int termOffset)
        {
            logMetaDataBuffer.Copy(termBuffer.Data + termOffset, LOG_DEFAULT_FRAME_HEADER_OFFSET, DataHeaderFlyweight.HEADER_LENGTH);
            //termBuffer.putBytes(termOffset, logMetaDataBuffer, LOG_DEFAULT_FRAME_HEADER_OFFSET, DataHeaderFlyweight.HEADER_LENGTH);
        }

        /**
         * Rotate the log and update the default headers for the new term.
         *
         * @param logPartitions     for the partitions of the log.
         * @param logMetaDataBuffer for the meta data.
         * @param activeIndex       current active index.
         * @param newTermId         to be used in the default headers.
         */
        public static void RotateLog(
            LogBufferPartition[] logPartitions,
            DirectBuffer logMetaDataBuffer,
            int activeIndex,
            int newTermId) {
            int nextIndex = NextPartitionIndex(activeIndex);
            int nextNextIndex = NextPartitionIndex(nextIndex);

            logPartitions[nextIndex].TermId = newTermId;
            logPartitions[nextNextIndex].Status = NEEDS_CLEANING;
            ActivePartitionIndex(logMetaDataBuffer, nextIndex);
        }

        /**
         * Set the initial value for the termId in the upper bits of the tail counter.
         *
         * @param termMetaData  contain the tail counter.
         * @param initialTermId to be set.
         */
        public static void InitialiseTailWithTermId(DirectBuffer termMetaData, int initialTermId) {
            termMetaData.WriteInt64(TERM_TAIL_COUNTER_OFFSET, ((long)initialTermId) << 32);
        }

        /**
         * Get the termId from a packed raw tail value.
         *
         * @param rawTail containing the termId
         * @return the termId from a packed raw tail value.
         */
        public static int TermId(long rawTail) {
            return (int)(rawTail >> 32);
        }

        /**
         * Read the termOffset from a packed raw tail value.
         *
         * @param rawTail    containing the termOffset.
         * @param termLength that the offset cannot exceed.
         * @return the termOffset value.
         */
        public static int TermOffset(long rawTail, long termLength) {
            long tail = rawTail & 0xFFFFFFFFL;

            return (int)Math.Min(tail, termLength);
        }
    }
}
