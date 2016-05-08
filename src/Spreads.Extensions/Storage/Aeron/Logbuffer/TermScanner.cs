using Spreads.Serialization;
using System;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
   
/**
 * Scans a term buffer for an availability range of messages.
 *
 * This can be used to concurrently read a term buffer which is being appended to.
 */
public class TermScanner
{
    /**
     * Scan the term buffer for availability of new messages from a given offset up to a maxLength of bytes.
     *
     * @param termBuffer to be scanned for new messages
     * @param offset     at which the scan should begin.
     * @param maxLength  in bytes of how much should be scanned.
     * @return resulting status of the scan which packs the available bytes and padding into a long.
     */
    public static  long ScanForAvailability(DirectBuffer termBuffer, int offset, int maxLength)
    {
        checked
        {
            maxLength = Math.Min(maxLength, (int) termBuffer.Length - offset);
            int available = 0;
            int padding = 0;

            do
            {
                int termOffset = offset + available;
                int frameLength =FrameDescriptor.FrameLengthVolatile(termBuffer, termOffset);
                if (frameLength <= 0)
                {
                    break;
                }

                int alignedFrameLength = BitUtil.Align(frameLength, FrameDescriptor.FRAME_ALIGNMENT);
                if (FrameDescriptor.IsPaddingFrame(termBuffer, termOffset))
                {
                    padding = alignedFrameLength - DataHeaderFlyweight.HEADER_LENGTH;
                    alignedFrameLength = DataHeaderFlyweight.HEADER_LENGTH;
                }

                available += alignedFrameLength;

                if (available > maxLength)
                {
                    available -= alignedFrameLength;
                    padding = 0;
                    break;
                }
            } while (0 == padding && available < maxLength);

            return Pack(padding, available);
        }
    }

    /**
     * Pack the values for available and padding into a long for returning on the stack.
     *
     * @param padding   value to be packed.
     * @param available value to be packed.
     * @return a long with both ints packed into it.
     */
    public static long Pack(int padding, int available)
    {
        return ((long)padding << 32) | (long)available;
    }

    /**
     * The number of bytes that are available to be read after a scan.
     *
     * @param result into which the padding value has been packed.
     * @return the count of bytes that are available to be read.
     */
    public static int Available(long result)
    {
        return (int)result;
    }

    /**
     * The count of bytes that should be added for padding to the position on top of what is available
     *
     * @param result into which the padding value has been packed.
     * @return the count of bytes that should be added for padding to the position on top of what is available.
     */
    public static int Padding(long result)
    {
        return (int)(result >> 32);
    }
}
}
