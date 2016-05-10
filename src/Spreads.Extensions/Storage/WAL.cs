using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Storage.Aeron;
using Spreads.Storage.Aeron.Logbuffer;

namespace Spreads.Storage {

    // Write-ahead log is based on Aeron LogBuffers
    // LogBuffers are permament, we always start from current position
    // Spreads protocol ensures that if a slow-producing series
    // has non-flushed mutations that are cleared from the LogBuffers,
    // then on next subscription to the series will force flushing
    // of chunks, after which subscribers will reveive chunk commands
    // and then Flush event, after which they will have syncronized data.

    internal interface IWAL : IDisposable {
        //void Append<T>(T message);
        BufferClaim Claim(int length);
        event OnAppend OnAppend;
    }

    internal class WAL {
        private readonly LogBuffers _logBuffers;

        private long _subscriberPosition;


        public WAL(string filepath, int bufferSizeMb = 10) {
            _logBuffers = new LogBuffers(filepath, bufferSizeMb * 1024 * 1024);
            var activePartitionIndex = LogBufferDescriptor.ActivePartitionIndex(_logBuffers.LogMetaData);
            var activePartition = _logBuffers.Partitions[activePartitionIndex];
            var rawTail = activePartition.RawTailVolatile;
            _subscriberPosition = rawTail;


            //_termAppender = new TermAppender()
        }


        private void Poll() {

        }


        /// <summary>
        /// TODO inline
        /// </summary>
        /// <param name="offsetBefore"></param>
        /// <param name="offsetAfter"></param>
        private void UpdatePosition(int offsetBefore, int offsetAfter) {
            _subscriberPosition = _subscriberPosition + (offsetAfter - offsetBefore);
        }

    }
}
