// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Utils;

namespace Spreads
{
    /// <summary>
    /// A simpler, faster, comparable and blittable replacement for GUID.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct UUID : IEquatable<UUID>, IComparable<UUID>
    {
        // opaque 16 bytes, ulongs help for equality/comparison and union fields in structs
        private readonly ulong _first;
        private readonly ulong _second;

        public UUID(ulong first, ulong second)
        {
            _first = first;
            _second = second;
        }

        // TODO! test if this is the same as reading directly from fb, endianness could affect this
        public UUID(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 16)
            {
                ThrowHelper.ThrowArgumentException("bytes == null || bytes.Length != 16");
            }
            fixed (byte* ptr = &bytes[0])
            {
                this = *(UUID*)ptr;
            }
        }

        internal ulong FirstHalfAsULong
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _first; }
        }

        internal ulong SecondHalfAsULong
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _second; }
        }

        [Obsolete("Use AsSpan() method")]
        public byte[] ToBytes()
        {
            var bytes = new byte[16];
            fixed (byte* ptr = &bytes[0])
            {
                *(UUID*)ptr = this;
            }
            return bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsSpan()
        {
            var ptr = Unsafe.AsPointer(ref System.Runtime.CompilerServices.Unsafe.AsRef<UUID>(in this));
            return new ReadOnlySpan<byte>(ptr, 16);
        }

        public UUID(Guid guid) : this(guid.ToByteArray())
        {
        }

        public UUID(string value) : this(value.MD5Bytes())
        {
        }

        [Obsolete]
        [SuppressMessage("ReSharper", "ImpureMethodCallOnReadonlyValueField")]
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
            return _first == other._first && _second == other._second;
        }

        public override int GetHashCode()
        {
            ulong mask = int.MaxValue;
            return (int)(_first & mask);
        }

        public override bool Equals(object obj)
        {
            return Equals((UUID)obj);
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