using Spreads.Buffers;
using Spreads.DataTypes;
using Spreads.Serialization.Utf8Json;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization
{
    // Read indivual values from serilized arrays without deserialization

    public unsafe struct SerializedMemoryIterator : IEnumerable<DirectBuffer>, IEnumerator<DirectBuffer>
    {
        private readonly Memory<byte> _memory;
        private readonly bool _isBinary;
        private MemoryHandle _handle;

        // private int _index;
        private int _offset;

        private int _length;
        private JsonReader _reader;

        public SerializedMemoryIterator(Memory<byte> memory, bool isBinary)
        {
            _memory = memory;
            _isBinary = isBinary;

            _handle = memory.Pin();

            // _index = -1;
            _offset = 0;
            _length = 0;
            if (!isBinary)
            {
                if (!MemoryMarshal.TryGetArray<byte>(memory, out var segment))
                {
                    ThrowHelper.ThrowInvalidOperationException("JSON data must be currently backed by a byte array. This is Utf8Json limitation.");
                }

                _reader = new JsonReader(segment.Array, segment.Offset);
            }
            else
            {
                _reader = default;
            }
        }

        public SerializedMemoryIterator GetEnumerator()
        {
            return new SerializedMemoryIterator(_memory, _isBinary);
        }

        IEnumerator<DirectBuffer> IEnumerable<DirectBuffer>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_isBinary)
            {
                // binary has always a header with fixed-size or an int after the header with payload size
                // note that this only supports individual values packed consequitively, not binary arrays

                _offset = _offset + _length;

                if (_offset >= _memory.Length)
                {
                    return false;
                }

                var header = ReadUnaligned<DataTypeHeader>(_handle.Pointer);
                if (header.IsFixedSize)
                {
                    _length = DataTypeHeader.Size + header.TypeSize;
                }
                else
                {
                    var payloadLength = ReadUnaligned<int>((void*)((IntPtr)_handle.Pointer + 4));
                    _length = DataTypeHeader.Size + 4 + payloadLength;
                }

                return true;
            }
            else
            {
                // segment is what is returned by ReadNextBlock
                if (_offset == 0)
                {
                    _reader.ReadIsBeginArrayWithVerify();
                    var subsegment = _reader.ReadNextBlockSegment();
                    _offset = subsegment.Offset;
                    _length = subsegment.Count;
                    return true;
                }

                if (_reader.ReadIsValueSeparator())
                {
                    if (_reader.ReadIsEndArray()) // [x, y, z,] - case of end separator
                    {
                        Trace.TraceWarning("We should not support this case when packing data");
                        return false;
                    }
                    var subsegment = _reader.ReadNextBlockSegment();
                    _offset = subsegment.Offset;
                    _length = subsegment.Count;
                    return true;
                }

                _reader.ReadIsEndArrayWithVerify();
                return false;
            }
        }

        public void Reset()
        {
            _offset = 0;
        }

        public DirectBuffer Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return new DirectBuffer(_length, (IntPtr)_handle.Pointer + _offset); }
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
