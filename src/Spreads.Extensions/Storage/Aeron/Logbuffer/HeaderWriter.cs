using System.Diagnostics;
using System.Threading;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage.Aeron.Logbuffer {
    /// <summary>
    /// Utility for applying a header to a message in a term buffer.
    /// 
    /// This class is designed to be thread safe to be used across multiple producers and makes the header
    /// visible in the correct order for consumers.
    /// </summary>
    public class HeaderWriter {
        private readonly DataHeader _defaultHeader;


        public HeaderWriter(DataHeader defaultHeader) {
            _defaultHeader = defaultHeader;
        }

        /// <summary>
        /// Write a header to the term buffer in {@link ByteOrder#LITTLE_ENDIAN} format using the minimum instructions.
        /// </summary>
        /// <param name="termBuffer">termBuffer to be written to.</param>
        /// <param name="offset">offset at which the header should be written.</param>
        /// <param name="length">length of the fragment including the header.</param>
        /// <param name="termId">termId of the current term buffer.</param>
        public unsafe void Write(DirectBuffer termBuffer, int offset, int length, int termId)
        {
            var dataHeader = _defaultHeader; // copy struct by value
            dataHeader.Header.FrameLength = -length;
            dataHeader.TermOffset = offset;
            dataHeader.TermID = termId;

            *(DataHeader*) (termBuffer.Data + offset + HeaderFlyweight.FRAME_LENGTH_FIELD_OFFSET) = dataHeader;

            // NB we do not actually need any fence here, a frame is committed only after its length is set to a positive value,
            // therefore only volatile write of the length is needed
            // In .NET, Volatile.Write generates a fence: http://stackoverflow.com/a/6932271/801189
            // //Thread.MemoryBarrier();
        }
    }

}
