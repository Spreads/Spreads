using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Logbuffer;

namespace Spreads.Storage.Aeron {
    /// <summary>
    /// Represents a replicated publication <seealso cref="Image"/> from a publisher to a <seealso cref="Subscription"/>.
    /// Each <seealso cref="Image"/> identifies a source publisher by session id.
    /// </summary>

    public class Image {
        private long _correlationId;
        private int _sessionId;
        private int _termLengthMask;
        private int _positionBitsToShift;
        private volatile bool _isClosed;

        private Position subscriberPosition;
        private DirectBuffer[] _termBuffers = new DirectBuffer[LogBufferDescriptor.PARTITION_COUNT];
        private Header _header;
        private ErrorHandler _errorHandler;
        private LogBuffers _logBuffers;
        private string _sourceIdentity;

        /**
         * Construct a new image over a log to represent a stream of messages from a {@link Publication}.
         *
         * @param subscription       to which this {@link Image} belongs.
         * @param sessionId          of the stream of messages.
         * @param subscriberPosition for indicating the position of the subscriber in the stream.
         * @param logBuffers         containing the stream of messages.
         * @param errorHandler       to be called if an error occurs when polling for messages.
         * @param sourceIdentity     of the source sending the stream of messages.
         * @param correlationId      of the request to the media driver.
         */
        public Image(
            Subscription subscription,
            int sessionId,
            Position subscriberPosition,
            LogBuffers logBuffers,
            ErrorHandler errorHandler,
            String sourceIdentity,
            long correlationId) {
            this.subscription = subscription;
            this.sessionId = sessionId;
            this.subscriberPosition = subscriberPosition;
            this.logBuffers = logBuffers;
            this.errorHandler = errorHandler;
            this.sourceIdentity = sourceIdentity;
            this.correlationId = correlationId;

            DirectBuffer[] buffers = logBuffers.atomicBuffers();
            System.arraycopy(buffers, 0, termBuffers, 0, PARTITION_COUNT);

            int termLength = logBuffers.termLength();
            this.termLengthMask = termLength - 1;
            this.positionBitsToShift = Integer.numberOfTrailingZeros(termLength);
            header = new Header(LogBufferDescriptor.initialTermId(buffers[LOG_META_DATA_SECTION_INDEX]), positionBitsToShift);
        }

        /**
         * Get the length in bytes for each term partition in the log buffer.
         *
         * @return the length in bytes for each term partition in the log buffer.
         */
        public int termBufferLength() {
            return logBuffers.termLength();
        }

        /**
         * The sessionId for the steam of messages.
         *
         * @return the sessionId for the steam of messages.
         */
        public int sessionId() {
            return sessionId;
        }

        /**
         * The source identity of the sending publisher as an abstract concept appropriate for the media.
         *
         * @return source identity of the sending publisher as an abstract concept appropriate for the media.
         */
        public String sourceIdentity() {
            return sourceIdentity;
        }

        /**
         * The initial term at which the stream started for this session.
         *
         * @return the initial term id.
         */
        public int initialTermId() {
            return header.initialTermId();
        }

        /**
         * The correlationId for identification of the image with the media driver.
         *
         * @return the correlationId for identification of the image with the media driver.
         */
        public long correlationId() {
            return correlationId;
        }

        /**
         * Get the {@link Subscription} to which this {@link Image} belongs.
         *
         * @return the {@link Subscription} to which this {@link Image} belongs.
         */
        public Subscription subscription() {
            return subscription;
        }

        /**
         * Has this object been closed and should no longer be used?
         *
         * @return true if it has been closed otherwise false.
         */
        public boolean isClosed() {
            return isClosed;
        }

        /**
         * The position this {@link Image} has been consumed to by the subscriber.
         *
         * @return the position this {@link Image} has been consumed to by the subscriber.
         */
        public long position() {
            if (isClosed) {
                return 0;
            }

            return subscriberPosition.get();
        }

        /**
         * The {@link FileChannel} to the raw log of the Image.
         *
         * @return the {@link FileChannel} to the raw log of the Image.
         */
        public FileChannel fileChannel() {
            return logBuffers.fileChannel();
        }

        /**
         * Poll for new messages in a stream. If new messages are found beyond the last consumed position then they
         * will be delivered to the {@link FragmentHandler} up to a limited number of fragments as specified.
         *
         * To assemble messages that span multiple fragments then use {@link FragmentAssembler}.
         *
         * @param fragmentHandler to which message fragments are delivered.
         * @param fragmentLimit   for the number of fragments to be consumed during one polling operation.
         * @return the number of fragments that have been consumed.
         * @see FragmentAssembler
         */
        public int poll(FragmentHandler fragmentHandler, int fragmentLimit) {
            if (isClosed) {
                return 0;
            }

            long position = subscriberPosition.get();
            int termOffset = (int)position & _termLengthMask;
            DirectBuffer termBuffer = activeTermBuffer(position);

            long outcome = TermReader.Read(termBuffer, termOffset, fragmentHandler, fragmentLimit, header, errorHandler);

            updatePosition(position, termOffset, TermReader.Offset(outcome));

            return TermReader.FragmentsRead(outcome);
        }

        /**
         * Poll for new messages in a stream. If new messages are found beyond the last consumed position then they
         * will be delivered to the {@link ControlledFragmentHandler} up to a limited number of fragments as specified.
         *
         * To assemble messages that span multiple fragments then use {@link ControlledFragmentAssembler}.
         *
         * @param fragmentHandler to which message fragments are delivered.
         * @param fragmentLimit   for the number of fragments to be consumed during one polling operation.
         * @return the number of fragments that have been consumed.
         * @see ControlledFragmentAssembler
         */
        public int controlledPoll(ControlledFragmentHandler fragmentHandler, int fragmentLimit) {
            if (isClosed) {
                return 0;
            }

            long position = subscriberPosition.get();
            int termOffset = (int)position & termLengthMask;
            int offset = termOffset;
            int fragmentsRead = 0;
            DirectBuffer termBuffer = activeTermBuffer(position);

            try {
                int capacity = termBuffer.capacity();
                do {
                    int length = frameLengthVolatile(termBuffer, offset);
                    if (length <= 0) {
                        break;
                    }

                    int frameOffset = offset;
                    int alignedLength = BitUtil.align(length, FRAME_ALIGNMENT);
                    offset += alignedLength;

                    if (!isPaddingFrame(termBuffer, frameOffset)) {
                        header.buffer(termBuffer);
                        header.offset(frameOffset);

                        Action action = fragmentHandler.onFragment(
                            termBuffer, frameOffset + HEADER_LENGTH, length - HEADER_LENGTH, header);

                        ++fragmentsRead;

                        if (action == BREAK) {
                            break;
                        } else if (action == ABORT) {
                            --fragmentsRead;
                            offset = frameOffset;
                            break;
                        } else if (action == COMMIT) {
                            position += alignedLength;
                            termOffset = offset;
                            subscriberPosition.setOrdered(position);
                        }
                    }
                }
                while (fragmentsRead < fragmentLimit && offset < capacity);
            } catch (Throwable t) {
                errorHandler.onError(t);
            }

            updatePosition(position, termOffset, offset);

            return fragmentsRead;
        }

        /**
         * Poll for new messages in a stream. If new messages are found beyond the last consumed position then they
         * will be delivered to the {@link BlockHandler} up to a limited number of bytes.
         *
         * @param blockHandler     to which block is delivered.
         * @param blockLengthLimit up to which a block may be in length.
         * @return the number of bytes that have been consumed.
         */
        public int blockPoll(BlockHandler blockHandler, int blockLengthLimit) {
            if (isClosed) {
                return 0;
            }

            long position = subscriberPosition.get();
            int termOffset = (int)position & termLengthMask;
            DirectBuffer termBuffer = activeTermBuffer(position);
            int limit = Math.min(termOffset + blockLengthLimit, termBuffer.capacity());

            int resultingOffset = TermBlockScanner.scan(termBuffer, termOffset, limit);

            int bytesConsumed = resultingOffset - termOffset;
            if (resultingOffset > termOffset) {
                try {
                    int termId = termBuffer.getInt(termOffset + TERM_ID_FIELD_OFFSET, LITTLE_ENDIAN);

                    blockHandler.onBlock(termBuffer, termOffset, bytesConsumed, sessionId, termId);
                } catch (Throwable t) {
                    errorHandler.onError(t);
                }

                subscriberPosition.setOrdered(position + bytesConsumed);
            }

            return bytesConsumed;
        }

        /**
         * Poll for new messages in a stream. If new messages are found beyond the last consumed position then they
         * will be delivered to the {@link FileBlockHandler} up to a limited number of bytes.
         *
         * @param fileBlockHandler to which block is delivered.
         * @param blockLengthLimit up to which a block may be in length.
         * @return the number of bytes that have been consumed.
         */
        public int filePoll(FileBlockHandler fileBlockHandler, int blockLengthLimit) {
            if (isClosed) {
                return 0;
            }

            long position = subscriberPosition.get();
            int termOffset = (int)position & termLengthMask;
            int activeIndex = indexByPosition(position, positionBitsToShift);
            DirectBuffer termBuffer = termBuffers[activeIndex];
            int capacity = termBuffer.capacity();
            int limit = Math.min(termOffset + blockLengthLimit, capacity);

            int resultingOffset = TermBlockScanner.scan(termBuffer, termOffset, limit);

            int bytesConsumed = resultingOffset - termOffset;
            if (resultingOffset > termOffset) {
                try {
                    long offset = ((long)capacity * activeIndex) + termOffset;
                    int termId = termBuffer.getInt(termOffset + TERM_ID_FIELD_OFFSET, LITTLE_ENDIAN);

                    fileBlockHandler.onBlock(logBuffers.fileChannel(), offset, bytesConsumed, sessionId, termId);
                } catch (Throwable t) {
                    errorHandler.onError(t);
                }

                subscriberPosition.setOrdered(position + bytesConsumed);
            }

            return bytesConsumed;
        }

        private void updatePosition(long positionBefore, int offsetBefore, int offsetAfter) {
            long position = positionBefore + (offsetAfter - offsetBefore);
            if (position > positionBefore) {
                subscriberPosition.setOrdered(position);
            }
        }

        private DirectBuffer activeTermBuffer(long position) {
            return termBuffers[indexByPosition(position, positionBitsToShift)];
        }

        ManagedResource managedResource() {
            isClosed = true;
            return new ImageManagedResource();
        }

        private class ImageManagedResource implements ManagedResource
        {
        private long timeOfLastStateChange = 0;

        public void timeOfLastStateChange(long time) {
            this.timeOfLastStateChange = time;
        }

        public long timeOfLastStateChange() {
            return timeOfLastStateChange;
        }

        public void delete() {
            logBuffers.close();
        }
    }
}

}
