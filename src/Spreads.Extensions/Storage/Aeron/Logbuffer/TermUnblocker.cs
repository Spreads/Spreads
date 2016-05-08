using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
    public enum TermUnblockerStatus {
        /// <summary>
        /// No action has been taken during operation. 
        /// </summary>
        NO_ACTION,

        /// <summary>
        /// The term has been unblocked so that the log can progress. 
        /// </summary>
        UNBLOCKED,

        /// <summary>
        /// The term has been unblocked from the offset until the end of the term. 
        /// </summary>
        UNBLOCKED_TO_END,
    }


    public class TermUnblocker {


        /// <summary>
        /// Attempt to unblock the current term at the current offset.
        ///         
        /// <ol>
        ///     <li>Current position length is &gt; 0, then return</li>
        ///     <li>Current position length is 0, scan forward by frame alignment until, one of the following:
        ///     <ol>
        ///         <li>reach a non-0 length, unblock up to indicated position (check original frame length for non-0)</li>
        ///         <li>reach end of term and tail position &gt;= end of term, unblock up to end of term (check original
        ///             frame length for non-0)
        ///         </li>
        ///         <li>reach tail position &lt; end of term, do NOT unblock</li>
        ///     </ol>
        ///     </li>
        /// </ol>
        /// </summary>
        /// <param name="logMetaDataBuffer"> containing the default headers </param>
        /// <param name="termBuffer">        to unblock </param>
        /// <param name="blockedOffset">     to unblock at </param>
        /// <param name="tailOffset">        to unblock up to </param>
        /// <param name="termId">            for the current term. </param>
        /// <returns> whether unblocking was done, not done, or applied to end of term </returns>

        public static TermUnblockerStatus Unblock(
            DirectBuffer logMetaDataBuffer,
            DirectBuffer termBuffer,
            int blockedOffset,
            int tailOffset,
            int termId) {
            var status = TermUnblockerStatus.NO_ACTION;
            int frameLength = FrameDescriptor.FrameLengthVolatile(termBuffer, blockedOffset);

            if (frameLength < 0) {
                ResetHeader(logMetaDataBuffer, termBuffer, blockedOffset, termId, -frameLength);
                status = TermUnblockerStatus.UNBLOCKED;
            } else if (0 == frameLength) {
                int currentOffset = blockedOffset + FrameDescriptor.FRAME_ALIGNMENT;

                while (currentOffset < tailOffset) {
                    frameLength = FrameDescriptor.FrameLengthVolatile(termBuffer, currentOffset);

                    if (frameLength != 0) {
                        if (ScanBackToConfirmZeroed(termBuffer, currentOffset, blockedOffset)) {
                            int length = currentOffset - blockedOffset;
                            ResetHeader(logMetaDataBuffer, termBuffer, blockedOffset, termId, length);
                            status = TermUnblockerStatus.UNBLOCKED;
                        }

                        break;
                    }

                    currentOffset += FrameDescriptor.FRAME_ALIGNMENT;
                }

                if (currentOffset == termBuffer.Length) {
                    if (0 == FrameDescriptor.FrameLengthVolatile(termBuffer, blockedOffset)) {
                        int length = currentOffset - blockedOffset;
                        ResetHeader(logMetaDataBuffer, termBuffer, blockedOffset, termId, length);
                        status = TermUnblockerStatus.UNBLOCKED_TO_END;
                    }
                }
            }

            return status;
        }

        private static void ResetHeader(
            DirectBuffer logMetaDataBuffer,
            DirectBuffer termBuffer,
            int termOffset,
            int termId,
            int frameLength) {
            LogBufferDescriptor.ApplyDefaultHeader(logMetaDataBuffer, termBuffer, termOffset);
            FrameDescriptor.FrameType(termBuffer, termOffset, HeaderFlyweight.HDR_TYPE_PAD);
            FrameDescriptor.FrameTermOffset(termBuffer, termOffset);
            FrameDescriptor.FrameTermId(termBuffer, termOffset, termId);
            FrameDescriptor.FrameLengthOrdered(termBuffer, termOffset, frameLength);
        }

        private static bool ScanBackToConfirmZeroed(DirectBuffer buffer, int from, int limit) {
            int i = from - FrameDescriptor.FRAME_ALIGNMENT;
            bool allZeros = true;
            while (i >= limit) {
                if (0 != FrameDescriptor.FrameLengthVolatile(buffer, i)) {
                    allZeros = false;
                    break;
                }

                i -= FrameDescriptor.FRAME_ALIGNMENT;
            }

            return allZeros;
        }
    }

}
