/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Runtime.InteropServices;
using Spreads.Utils;

namespace Spreads {

    /// <summary>
    /// A simpler, faster, comparable and blittable replacement for Guid.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct UUID : IEquatable<UUID>, IComparable<UUID> {
        private ulong _first;
        private ulong _second;

        public UUID(ulong first, ulong second) {
            _first = first;
            _second = second;
        }

        // TODO! test if this is the same as reading directly from fb, endianness could affect this
        public UUID(byte[] bytes) {
            if (bytes == null || bytes.Length < 16) throw new ArgumentException("bytes == null || bytes.Length < 16", nameof(bytes));
            fixed (byte* ptr = &bytes[0]) {
                _first = *(ulong*)ptr;
                _second = *(ulong*)(ptr + 8);
            }
        }

        public byte[] ToBytes() {
            var bytes = new byte[16];
            fixed (byte* ptr = &bytes[0]) {
                *(ulong*)ptr = _first;
                *(ulong*)(ptr + 8) = _second;
            }
            return bytes;
        }

        public UUID(Guid guid) : this(guid.ToByteArray()) { }
        public UUID(string value) : this(value.MD5Bytes()) { }

        public int CompareTo(UUID other) {
            var f = _first.CompareTo(other._first);
            if (f == 0) {
                return _second.CompareTo(other._second);
            }
            return f;
        }

        public bool Equals(UUID other) {
            return this._first == other._first && this._second == other._second;
        }

        public override int GetHashCode() {
            ulong mask = int.MaxValue;
            return (int)(_first & mask);
        }

        public override bool Equals(object obj) {
            return this.Equals((UUID)obj);
        }


        public static bool operator ==(UUID first, UUID second) {
            return first.Equals(second);
        }

        public static bool operator !=(UUID first, UUID second) {
            return !(first == second);
        }
    }
}
