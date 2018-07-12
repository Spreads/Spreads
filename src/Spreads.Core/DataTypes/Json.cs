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
    /// Wrapper for UTF8 json string
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    [StructLayout(LayoutKind.Auto)]
    [JsonFormatter(typeof(Formatter))]
    public readonly struct Json
    {
        private readonly ArraySegment<byte> _segment;
        private readonly string _string;

        public Json(ArraySegment<byte> segment)
        {
            _segment = segment;
            _string = null;
        }

        public Json(string utf16JsonString)
        {
            _segment = default;
            _string = utf16JsonString;
        }

        public override string ToString()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            return _string ?? Encoding.UTF8.GetString(_segment.Array, _segment.Offset, _segment.Count);
        }

        public T Deserialize<T>()
        {
            return JsonSerializer.Deserialize<T>(_segment.Array, _segment.Offset);
        }

        internal unsafe class Formatter : IJsonFormatter<Json>
        {
            public void Serialize(ref JsonWriter writer, Json value, IJsonFormatterResolver formatterResolver)
            {
                if (value._string != null)
                {
                    var maxLen = Encoding.UTF8.GetMaxByteCount(value._string.Length);
                    var useShared = maxLen <= BufferPool.SharedBuffer.Array.Length;
                    var buffer = useShared
                        ? BufferPool.SharedBuffer.Array
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
