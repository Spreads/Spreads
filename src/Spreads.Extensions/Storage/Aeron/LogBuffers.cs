using System;
using System.IO;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Logbuffer;

namespace Spreads.Storage.Aeron {
    /// <summary>
    /// Takes a log file name and maps the file into memory and wraps it with <seealso cref="DirectBuffer"/>s as appropriate.
    /// </summary>
    /// <seealso> cref="LogBufferDescriptor" </seealso>
    public class LogBuffers : IDisposable {
        private readonly int _termLength;
        private DirectFile _df;
        private readonly DirectBuffer[] _buffers = new DirectBuffer[(LogBufferDescriptor.PARTITION_COUNT * 2) + 1];

        public LogBuffers(string logFileName, int termLength = LogBufferDescriptor.TERM_MIN_LENGTH) {
            try {

                long logLength = LogBufferDescriptor.PARTITION_COUNT *
                                 (LogBufferDescriptor.TERM_META_DATA_LENGTH + termLength) +
                                 LogBufferDescriptor.LOG_META_DATA_LENGTH;
                termLength = LogBufferDescriptor.ComputeTermLength(logLength);
                LogBufferDescriptor.CheckTermLength(termLength);
                _df = new DirectFile(logFileName, logLength);
                _termLength = termLength;

                // if log length exceeds MAX_INT we need multiple mapped buffers, (see FileChannel.map doc).
                if (logLength < int.MaxValue) {

                    int metaDataSectionOffset = termLength * LogBufferDescriptor.PARTITION_COUNT;

                    for (int i = 0; i < LogBufferDescriptor.PARTITION_COUNT; i++) {
                        int metaDataOffset = metaDataSectionOffset + (i * LogBufferDescriptor.TERM_META_DATA_LENGTH);

                        _buffers[i] = new DirectBuffer(termLength, _df.Buffer.Data + i * termLength);
                        _buffers[i + LogBufferDescriptor.PARTITION_COUNT] = new DirectBuffer(LogBufferDescriptor.TERM_META_DATA_LENGTH, _df.Buffer.Data + metaDataOffset);
                    }

                    _buffers[_buffers.Length - 1] = new DirectBuffer(LogBufferDescriptor.LOG_META_DATA_LENGTH,
                        _df.Buffer.Data + (int)(logLength - LogBufferDescriptor.LOG_META_DATA_LENGTH));
                } else {
                    throw new NotImplementedException("TODO Check .NET mapping limit");
                }
            } catch (IOException ex) {
                throw new AggregateException(ex);
            }

            foreach (var buffer in _buffers) {
                buffer.VerifyAlignment(8);
            }
        }

        public DirectBuffer[] Buffers() {
            return _buffers;
        }

        public DirectFile DirectFile() {
            return _df;
        }

        public void Dispose() {
            _df.Dispose();
            //foreach (var buffer in _buffers)
            //{
            //    buffer.Dispose();
            //}
        }

        public int TermLength => _termLength;
    }
}
