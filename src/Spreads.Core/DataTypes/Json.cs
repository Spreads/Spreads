// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Serialization.Utf8Json;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Spreads.DataTypes
{
    /// <summary>
    /// Wrapper for UTF8 JSON string.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Auto)]
    [JsonFormatter(typeof(Formatter))]
    public struct Json
    {
        private readonly ArraySegment<byte> _segment;
        private string _string;
        private readonly DirectBuffer _db;

        public Json(ArraySegment<byte> segment)
        {
            _segment = segment;
            _string = null;
            _db = DirectBuffer.Invalid;
        }

        public Json(string utf16JsonString)
        {
            _segment = default;
            _string = utf16JsonString;
            _db = DirectBuffer.Invalid;
        }

        public Json(DirectBuffer directBuffer)
        {
            _segment = default;
            _string = null;
            _db = directBuffer;
        }

        public override string ToString()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return _string ?? (_string = (_db.IsValid ? Encoding.UTF8.GetString(_db) : Encoding.UTF8.GetString(_segment.Array, _segment.Offset, _segment.Count)));
        }

        public T Deserialize<T>()
        {
            if (_string != null)
            {
                return JsonSerializer.Deserialize<T>(_string);
            }

            if (_db.IsValid)
            {
                return JsonSerializer.Deserialize<T>(_db);
            }

            return JsonSerializer.Deserialize<T>(_segment);
        }

        internal unsafe class Formatter : IJsonFormatter<Json>
        {
            public void Serialize(ref JsonWriter writer, Json value, IJsonFormatterResolver formatterResolver)
            {
                if (value._db.IsValid)
                {
                    var _ = ToString(); // side effect TODO Add DB/Memory overloads to WriteRawSegment
                }
                
                if (value._string != null)
                {
                    var maxLen = Encoding.UTF8.GetMaxByteCount(value._string.Length);
                    var useShared = maxLen <= JsonSerializer.MemoryPool.buffer.Length;
                    var buffer = useShared
                        ? JsonSerializer.MemoryPool.buffer
                        : BufferPool<byte>.Rent(maxLen);

                    fixed (char* charPtr = value._string)
                    fixed (byte* ptr = &buffer[0])
                    {
                        var len = Encoding.UTF8.GetBytes(charPtr, value._string.Length, ptr, maxLen);
                        writer.WriteRawSegment(new ArraySegment<byte>(buffer, 0, len));
                    }

                    if (!useShared)
                    {
                        BufferPool<byte>.Return(buffer, true);
                    }
                }
                else
                {
                    writer.WriteRawSegment(value._segment);
                }
            }

            public Json Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
            {
                return new Json(reader.ReadNextBlockSegment());
            }
        }
    }
}
