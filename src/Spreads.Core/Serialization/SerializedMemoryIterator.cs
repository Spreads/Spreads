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

        private readonly int _payloadSize;
        private readonly DataTypeHeader _header;
        private readonly DataTypeHeader _itemHeader;

        public SerializedMemoryIterator(Memory<byte> memory, bool isBinary)
        {
            _memory = memory;
            _isBinary = isBinary;

            _handle = memory.Pin();

            _offset = 0;
            _length = 0;

            _payloadSize = 0;
            _header = default;
            _itemHeader = default;

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
                _header = ReadUnaligned<DataTypeHeader>((void*)((IntPtr)_handle.Pointer));
                _itemHeader = new DataTypeHeader()
                {
                    TypeSize = _header.TypeSize,
                    TypeEnum = _header.ElementTypeEnum,
                    VersionAndFlags = _header.VersionAndFlags
                };
                if (_header.VersionAndFlags.IsBinary)
                {
                    if (_header.TypeSize <= 0)
                    {
                        ThrowHelper.ThrowInvalidOperationException("Arrays could be serialized as binary only if the type size is fixed.");
                    }
                    _payloadSize = ReadUnaligned<int>((void*)((IntPtr)_handle.Pointer + DataTypeHeader.Size));
                    _length = _header.TypeSize;
                    _reader = default;
                }
                else
                {
                    if (!MemoryMarshal.TryGetArray<byte>(memory, out var segment))
                    {
                        ThrowHelper.ThrowInvalidOperationException("JSON data must be currently backed by a byte array. This is Utf8Json limitation.");
                    }
                    _reader = new JsonReader(segment.Array, segment.Offset + 8);
                    _isBinary = false;
                }
            }
        }

        public SerializedMemoryIterator GetEnumerator()
        {
            return this;
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
                if (_offset == 0)
                {
                    _offset = DataTypeHeader.Size + 4;
                }
                else
                {
                    _offset += _length;
                }
                if (_offset >= 8 + _payloadSize)
                {
                    return false;
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

        public DataTypeHeader ItemHeader
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _itemHeader; }
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
