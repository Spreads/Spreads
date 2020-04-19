// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.DataTypes
{
    /// <summary>
    /// GUID-like structure that do not promise any RFC compliance and
    /// could be treated as securely random 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [BinarySerialization(16)]
    public readonly unsafe struct UUID : IEquatable<UUID>, IComparable<UUID>
    {
        // opaque 16 bytes
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly Guid _guid;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UUID NewUUID()
        {
            return new UUID(Guid.NewGuid());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UUID(Guid guid)
        {
            _guid = guid;
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
            var ptr = Unsafe.AsPointer(ref Unsafe.AsRef(in this));
            return new ReadOnlySpan<byte>(ptr, 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(UUID other)
        {
            var ptr = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(in this));
            var ptrOther = (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(in other));

            for (int i = 0; i < 16; i++)
            {
                var c = *(ptr + i) - *(ptrOther + i);
                if (c != 0)
                {
                    return c;
                }
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(UUID other)
        {
            var ptr = Unsafe.AsPointer(ref Unsafe.AsRef(in this));
            var ptrOther = Unsafe.AsPointer(ref Unsafe.AsRef(in other));

            return Unsafe.ReadUnaligned<long>(ptr) == Unsafe.ReadUnaligned<long>(ptrOther)
                   && Unsafe.ReadUnaligned<long>((byte*)ptr + 8) == Unsafe.ReadUnaligned<long>((byte*)ptrOther + 8);
        }

        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return obj is UUID other && Equals(other);
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