// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Utils;

namespace Spreads
{
    /// <summary>
    /// A simpler, faster, comparable and blittable replacement for Guid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UUID : IEquatable<UUID>, IComparable<UUID>
    {
        private ulong _first;
        private ulong _second;

        public UUID(ulong first, ulong second)
        {
            _first = first;
            _second = second;
        }

        // TODO! test if this is the same as reading directly from fb, endianness could affect this
        public UUID(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16) throw new ArgumentException("bytes == null || bytes.Length < 16", nameof(bytes));
            fixed (byte* ptr = &bytes[0])
            {
                _first = *(ulong*)ptr;
                _second = *(ulong*)(ptr + 8);
            }
        }

        [Obsolete("Use AsSpan() method")]
        public byte[] ToBytes()
        {
            var bytes = new byte[16];
            fixed (byte* ptr = &bytes[0])
            {
                *(ulong*)ptr = _first;
                *(ulong*)(ptr + 8) = _second;
            }
            return bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref this);
            return new Span<byte>(ptr, 16);
        }

        public UUID(Guid guid) : this(guid.ToByteArray())
        {
        }

        public UUID(string value) : this(value.MD5Bytes())
        {
        }

        public int CompareTo(UUID other)
        {
            var f = _first.CompareTo(other._first);
            if (f == 0)
            {
                return _second.CompareTo(other._second);
            }
            return f;
        }

        public bool Equals(UUID other)
        {
            return this._first == other._first && this._second == other._second;
        }

        public override int GetHashCode()
        {
            ulong mask = int.MaxValue;
            return (int)(_first & mask);
        }

        public override bool Equals(object obj)
        {
            return this.Equals((UUID)obj);
        }

        public static bool operator ==(UUID first, UUID second)
        {
            return first.Equals(second);
        }

        public static bool operator !=(UUID first, UUID second)
        {
            return !(first == second);
        }
    }
}