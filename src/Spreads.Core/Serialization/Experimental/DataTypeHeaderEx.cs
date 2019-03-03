// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using Spreads.Collections.Internal.Experimental;
using Spreads.DataTypes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization.Experimental
{
    /// <summary>
    /// <see cref="Variant"/> header. Since variant does not need serialization
    /// version and flags it could store an additional subtype information
    /// e.g. for <see cref="Frame{TRow,TCol,T}"/>.
    ///
    /// <para />
    ///
    /// See <see cref="DataTypeHeaderEx"/> for more details.
    /// </summary>
    /// <remarks>
    /// This is a mini-schema that very often is enough to describe all type information
    /// about data.
    /// 
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |     TEOFS     |     TEOFS1    |     TEOFS2    |     TEOFS3    |
    /// +---------------------------------------------------------------+
    ///
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = Size)]
    public struct VariantHeader
    {
        public const int Size = 4;

        public TypeEnumOrFixedSize TEOFS;
        public TypeEnumOrFixedSize TEOFS1;
        public TypeEnumOrFixedSize TEOFS2;
        public TypeEnumOrFixedSize TEOFS3;
    }

    /// <summary>
    /// DataType header for serialized data. First byte contains serialization format flags.
    /// Bytes 1-3 describe the serialized type and its subtypes (for composites and containers).
    /// Type information is stored as <see cref="TypeEnumOrFixedSize"/> 1-byte struct.
    /// </summary>
    /// <remarks>
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// | Version+Flags |     TEOFS     |     TEOFS1    |     TEOFS2    |  |Some containers have TEOFS3 but it is a part of payload. |
    /// +---------------------------------------------------------------+
    ///
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = Size)]
    public struct DataTypeHeaderEx
    {
        public const int Size = 4;

        // First 4 bytes are always the same
        internal const int VersionAndFlagsOffset = 0;

        [FieldOffset(VersionAndFlagsOffset)]
        public VersionAndFlags VersionAndFlags;

        internal const int TeofsOffset = 1;

        [FieldOffset(TeofsOffset)]
        public TypeEnumOrFixedSize TEOFS;

        internal const int Teofs1Offset = 1;

        [FieldOffset(Teofs1Offset)]
        public TypeEnumOrFixedSize TEOFS1;

        internal const int Teofs2Offset = 1;

        [FieldOffset(Teofs2Offset)]
        public TypeEnumOrFixedSize TEOFS2;


        // TODO (?) HasTEOFS3 bool property and BufferReader method to read VariantHeader


        private int FixedSize()
        {
            var size = TEOFS.Size;
            if (size > 0)
            {
                return size;
            }

            return FixedSizeComposite();
        }

        private int FixedSizeComposite()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Positive number if a type has fixed size.
        /// </summary>
        public int TypeSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FixedSize();
        }

        public bool IsTypeFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => FixedSize() > 0;
        }

        public unsafe int FirstPayloadByteOffset
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var isVarSize = !IsTypeFixedSize;
                var offset = ((int)(*(byte*)&isVarSize) << 2); // 4 for varsize or 0 for fixed size
                return Size + offset;
            }
        }

        public unsafe int FirstPayloadByteOffsetExTimeStamp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var tsLen = VersionAndFlags.TimestampFlagMask &
                            *(byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this);
                return FirstPayloadByteOffset + tsLen;
            }
        }
    }
}