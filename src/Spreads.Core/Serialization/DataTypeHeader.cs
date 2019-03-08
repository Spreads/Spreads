using Spreads.DataTypes;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// DataType header
    /// </summary>
    /// <remarks>
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// | Version+Flags |    TypeEnum   |    TypeSize   |  SubTypeEnum  |
    /// +---------------------------------------------------------------+
    ///
    ///
    /// In some places we access the elements directly by byte, without dereferencing the entire struct
    /// Also this is already in a lot of persisted data, no chance to change this. Version slot applies
    /// only to data layout *after* this header.
    /// </remarks>
    [Obsolete("Use DataTypeHeaderEx")]
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = Size)]
    public struct DataTypeHeader
    {
        public const int Size = 4;

        // First 4 bytes are always the same
        internal const int VersionAndFlagsOffset = 0;

        [FieldOffset(VersionAndFlagsOffset)]
        public VersionAndFlags VersionAndFlags;

        internal const int TypeEnumOffset = 1;

        [FieldOffset(TypeEnumOffset)]
        public TypeEnum TypeEnum;

        internal const int TypeSizeOffset = 2;

        /// <summary>
        /// Size of fixed binary or array element type. If size if fixed then this should be positive.
        /// </summary>
        [FieldOffset(TypeSizeOffset)]
        public byte TypeSize;

        internal const int ElementTypeEnumOffset = 3;

        [FieldOffset(ElementTypeEnumOffset)]
        public TypeEnum ElementTypeEnum;

        public bool IsTypeFixedSize
        {
#if SPREADS
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // LE, if equal than this is unknown but fixed size. TypeSize could be > 0 for containers (e.g. arrays), so need to check type enum
            get => unchecked((uint)(TypeEnum - 1)) < Variant.KnownSmallTypesLimit;
#else
            get => unchecked((uint)(TypeEnum - 1)) < 64;
#endif
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
