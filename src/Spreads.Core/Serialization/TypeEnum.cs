// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.Serialization
{
    /// <summary>
    /// Known types and containers enumeration.
    /// </summary>
    /// <remarks>
    /// The goal of this enumeration is to have a unique small ids for frequently used
    /// types, provision ids for likely future types and set ids to main containers.
    ///
    /// <para />
    ///
    /// Serialized data or non-generic containers such as <see cref="Variant"/>
    /// have <see cref="DataTypeHeader"/>
    /// that contains 3 <see cref="TypeEnum"/> slots that describe the data.
    ///
    /// <para />
    ///
    /// Integer types are always serialized as little-endian.
    /// Big-endian is completely not and won't be supported in foreseeable future.
    /// </remarks>
    public enum TypeEnum : byte
    {
        // Note: TypeEnum must be enough to create a correct non-generic System.Object from a raw byte pointer.

        /// <summary>
        /// Unit type, meaningful absence of any value.
        /// Compare to default/null which are a special values of some type.
        /// </summary>
        None = 0,

        #region Fixed-length known scalar types

        // Note: we could add composite structs if they are always used as scalars,
        // have fixed-size and are frequently used. Candidates are:
        // * Complex32/64 - two float32/64
        // * Quote - price+quantity, however both price and quantity could be fractional and this looks more like a tuple.
        // * Geometry - Circle, Point2d, Point3d, but they are TupleT2 where T is stored as subtype 1.
        // It will make sense to add some tuple-like structures if tuple processing is slower and the types are perf critical.
        // However often in that case they are stored as arrays and header processing is amortized.
        // We need a case where we could store 1 byte instead of 4. VersionAndFlag define context that is common.

        /// <summary>
        /// See <see cref="sbyte"/>.
        /// </summary>
        Int8 = 1,

        /// <summary>
        /// See <see cref="short"/>.
        /// </summary>
        Int16 = 2,

        /// <summary>
        /// See <see cref="int"/>.
        /// </summary>
        Int32 = 3,

        /// <summary>
        /// See <see cref="long"/>.
        /// </summary>
        Int64 = 4,

        /// <summary>
        ///
        /// </summary>
        Int128 = 5,

        /// <summary>
        /// See <see cref="byte"/>.
        /// </summary>
        UInt8 = 6,

        /// <summary>
        /// See <see cref="ushort"/>.
        /// </summary>
        UInt16 = 7,

        /// <summary>
        /// See <see cref="uint"/>.
        /// </summary>
        UInt32 = 8,

        /// <summary>
        /// See <see cref="ulong"/>.
        /// </summary>
        UInt64 = 9,

        /// <summary>
        ///
        /// </summary>
        UInt128 = 10,

        // IEEE 754-2008 https://en.wikipedia.org/wiki/IEEE_754-2008_revision

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Half-precision_floating-point_format.
        /// </summary>
        Float16 = 11,

        /// <summary>
        /// See <see cref="float"/>.
        /// </summary>
        Float32 = 12,

        /// <summary>
        /// See <see cref="double"/>.
        /// </summary>
        Float64 = 13,

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Quadruple-precision_floating-point_format.
        /// </summary>
        Float128 = 14,

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Decimal32_floating-point_format.
        /// </summary>
        Decimal32 = 15,

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Decimal64_floating-point_format.
        /// </summary>
        Decimal64 = 16,

        /// <summary>
        /// See https://en.wikipedia.org/wiki/Decimal128_floating-point_format.
        /// </summary>
        Decimal128 = 17,

        /// <summary>
        /// See <see cref="decimal"/>.
        /// </summary>
        Decimal = 18,

        /// <summary>
        /// See <see cref="SmallDecimal"/>.
        /// </summary>
        SmallDecimal = 19,

        /// <summary>
        /// See <see cref="bool"/>.
        /// </summary>
        Bool = 20,

        /// <summary>
        /// See <see cref="char"/>
        /// </summary>
        Utf16Char = 21,

        /// <summary>
        /// See <see cref="UUID"/>. Could store <see cref="Guid"/> as well, but there is no restrictions on format.
        /// </summary>
        UUID = 22,

        /// <summary>
        /// <see cref="DateTime"/> UTC ticks (100ns intervals since zero) as UInt64
        /// </summary>
        DateTime = 23,

        /// <summary>
        /// <see cref="Timestamp"/> as nanoseconds since Unix epoch as UInt64
        /// </summary>
        Timestamp = 24,

        Symbol = 31,

        // Try to keep scalars with sizes <= 16 below 32.

        Symbol32 = 32,
        Symbol64 = 33,
        Symbol128 = 34,
        Symbol256 = 35,

        // ----------------------------------------------------------------
        // Comparison [(byte)(TypeEnum) < 64 = true] means known fixed type
        // ----------------------------------------------------------------

        #endregion Fixed-length known scalar types

        #region Variable size known types

        /// <summary>
        /// Opaque binary string prefixed by <see cref="int"/> length in bytes.
        /// Alias for <see cref="Array"/> of <see cref="UInt8"/>.
        /// </summary>
        Binary = 64,

        /// <summary>
        /// A Utf8 string prefixed by <see cref="int"/> length in bytes.
        /// </summary>
        Utf8String = 65, // this is not exactly

        /// <summary>
        /// A Utf16 <see cref="string"/> prefixed by <see cref="int"/> length in bytes.
        /// Alias for <see cref="Array"/> of <see cref="Utf16Char"/>.
        /// </summary>
        Utf16String = 66,

        /// <summary>
        /// Utf8 JSON prefixed by <see cref="int"/> length in bytes.
        /// </summary>
        Json = 67,

        #endregion Variable size known types

        #region Tuple-like structures that do not need TEOFS3

        /// <summary>
        /// N ordered elements of the same type with N up to 256 elements.
        /// </summary>
        /// <remarks>
        /// This covers all same-type fixed-size tuples from 1 to 256.
        /// The number of elements is stored in <see cref="DataTypeHeader.TupleNCount"/> (<see cref="DataTypeHeader.TEOFS1"/>).
        /// Element type is stored in <see cref="DataTypeHeader.TupleTNTeofs"/> (<see cref="DataTypeHeader.TEOFS2"/>).
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleTN    |  TupleTNCount |       T       |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is not a scalar (see <see cref="DataTypeHeader.IsScalar"/>)
        /// then T slot is set to <see cref="CompositeType"/>.
        ///
        /// </remarks>
        /// <seealso cref="TupleN"/>
        /// <seealso cref="ArrayOfTupleTN"/>
        // ReSharper disable once InconsistentNaming
        TupleTN = 70, // Note: FixedArray without T is Schema.

        /// <summary>
        /// A tuple with two elements of same type.
        /// </summary>
        /// <remarks>
        /// Element type is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleT2    |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        TupleT2 = 71,

        /// <summary>
        /// A tuple with three elements of same type.
        /// </summary>
        /// <remarks>
        /// Element type is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleT3    |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        TupleT3 = 72,

        /// <summary>
        /// A tuple with four elements of same type.
        /// </summary>
        /// <remarks>
        /// Element type is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleT4    |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        TupleT4 = 73,

        /// <summary>
        /// A tuple with five elements of same type.
        /// </summary>
        /// <remarks>
        /// Element type is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleT5    |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        TupleT5 = 74,

        /// <summary>
        /// A tuple with six elements of same type.
        /// </summary>
        /// <remarks>
        /// Element type is stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |    TupleT6    |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        TupleT6 = 75,

        /// <summary>
        /// A tuple with two elements of different types.
        /// </summary>
        /// <remarks>
        /// Element types are stored in <see cref="DataTypeHeader.TEOFS1"/>
        /// and <see cref="DataTypeHeader.TEOFS2"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     Tuple2    |       T1      |       T2      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If types T1 or T2 is not a scalar type (see <see cref="DataTypeHeader.IsScalar"/>)
        /// its slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        Tuple2 = 76,

        /// <summary>
        /// A tuple with two elements of different types prefixed with <see cref="byte"/> tag.
        /// </summary>
        /// <remarks>
        /// Element types are stored in <see cref="DataTypeHeader.TEOFS1"/>
        /// and <see cref="DataTypeHeader.TEOFS2"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     Tuple2    |       T1      |       T2      |
        /// +---------------------------------------------------------------+
        ///
        /// followed by
        ///
        /// [Optional Timestamp] | [Optional Schemas] | [Byte Tag] | [T1 Payload] | [T2 Payload]
        /// ```
        /// If types T1 or T2 are not a scalar type (see <see cref="DataTypeHeader.IsScalar"/>)
        /// their slot is set to <see cref="CompositeType"/>.
        /// </remarks>
        Tuple2Byte = 77, // If we need a tag with a different type use Tuple3

        /// <summary>
        /// Same as <see cref="Tuple2Byte"/> but with <see cref="long"/> prefix.
        /// </summary>
        Tuple2Long = 78,

        // TODO return here when start working with frames.

        /// <summary>
        /// Row key type in TEOFS1, value type in TEOFS2. Always followed by <see cref="int"/> column index.
        /// Used for <see cref="Matrix{T}"/> and <see cref="Frame{TRow,TCol}"/> in-place value updates.
        /// </summary>
        [Obsolete("Not used actually. Just an idea how to serialize updates.")]
        KeyIndexValue = 79,

        #endregion Tuple-like structures that do not need TEOFS3

        #region Containers with 1-2 subtypes

        /// <summary>
        /// A variable-length sequence of elements of the same type.
        /// </summary>
        /// <remarks>
        /// Element type stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     Array     |       T       |      T1?      |
        /// +---------------------------------------------------------------+
        /// ```
        /// If type T is a scalar type (see <see cref="DataTypeHeader.IsScalar"/>) then
        /// T1 slot is empty. T1 could be a subtype of T if two slots is enough to
        /// describe the type T. If more than two slots are required to
        /// describe the type T then T slot is set to <see cref="CompositeType"/>.
        ///
        /// </remarks>
        Array = 80,

        /// <summary>
        ///
        /// </summary>
        /// <remarks>
        /// Element type stored in <see cref="DataTypeHeader.TEOFS1"/>.
        /// Dimension is stored in <see cref="DataTypeHeader.TEOFS2"/>.
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |  JaggedArray  |       T       |   Dimension   |
        /// +---------------------------------------------------------------+
        /// ```
        /// If element type is not a scalar type then T slot is set to <see cref="CompositeType"/>.
        ///
        /// </remarks>
        JaggedArray = 81,

        /// <summary>
        /// A variable-length sequence of <see cref="TupleTN"/> elements of the same type.
        /// </summary>
        /// <remarks>
        /// This type is common (e.g. it's a by-row part of a matrix) and we need to fuse <see cref="Array"/> and <see cref="TupleTN"/>
        /// first slot.
        /// The number of inner array elements is stored in <see cref="DataTypeHeader.TupleNCount"/> (<see cref="DataTypeHeader.TEOFS1"/>).
        /// Element type is stored in <see cref="DataTypeHeader.TupleTNTeofs"/> (<see cref="DataTypeHeader.TEOFS2"/>).
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags | ArrayOfTupleTN|  TupleTNCount |       T       |
        /// +---------------------------------------------------------------+
        /// ```
        /// </remarks>
        /// <seealso cref="TupleTN"/>
        // ReSharper disable once InconsistentNaming
        ArrayOfTupleTN = 82,

        // TODO number of dimensions is runtime parameter or not? Should it be in payload header?
        /// <summary>
        ///
        /// </summary>
        [Obsolete("No specs, just a placeholder.")]
        NDArray = 83, // same as array must have a subtype defined

        /// <summary>
        /// Table is a frame with <see cref="string"/> row and column keys and <see cref="Variant"/> data type.
        /// </summary>
        Table = 84,

        /// <summary>
        /// <see cref="Array"/> of <see cref="Tuple2"/> with unique keys.
        /// </summary>
        /// <remarks>
        ///
        /// Key type is stored in <see cref="DataTypeHeader.TEOFS1"/>
        /// and value type is stored in <see cref="DataTypeHeader.TEOFS2"/>.
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |      Map      |      TKey     |     TValue    |
        /// +---------------------------------------------------------------+
        /// ```
        ///
        /// </remarks>
        Map = 85, // Need this to fit type info in 3 TEOFS as in DataTypeHeader and avoid TEOFS3 in VariantHeader.

        /// <summary>
        /// A series or two-array map (dictionary). A <see cref="Tuple2"/> of two <see cref="Array"/>s.
        /// </summary>
        /// <remarks>
        /// Note that when both types are blittable then <see cref="Map"/> to/from <see cref="Series"/>
        /// conversion could be done via shuffle/unshuffle very quickly.
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     Series    |      TKey     |     TValue    |
        /// +---------------------------------------------------------------+
        /// ```
        ///
        /// </remarks>
        Series = 86,

        /// <summary>
        ///
        /// </summary>
        /// <remarks>
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     Frame     |    TRowKey    |   TColumnKey  |
        /// +---------------------------------------------------------------+
        /// ```
        /// </remarks>
        Frame = 87,

        #endregion Containers with 1-2 subtypes

        #region Types that require 3 subtypes

        /// <summary>
        /// A tuple with N elements of different types.
        /// </summary>
        /// <remarks>
        /// TupleN could have fixed size, e.g. (Timestamp,int,long,double).
        /// If all elements of such tuple are fixed-size scalars and their
        /// total size is no more than 256 bytes than <see cref="DataTypeHeader.TupleNFixedSize"/>
        /// field (the slot <see cref="DataTypeHeader.TEOFS2"/>) has the total size.
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |     TupleN    |  TupleNCount  |   FixedSize   |
        /// +---------------------------------------------------------------+
        /// ```
        ///
        /// </remarks>
        /// <seealso cref="TupleTN"/>
        TupleN = 90,

        #endregion Types that require 3 subtypes

        // 100-119 reserved for internal use (so far).

        /// <summary>
        /// See <see cref="Variant"/>. Has own <see cref="TypeEnum"/> values before payload. // TODO spec that
        /// </summary>
        Variant = 120,

        /// <summary>
        /// A type needs a generic context or stores a schema info somewhere in application context.
        /// </summary>
        CompositeType = 125,

        /// <summary>
        /// A custom user type that could have a binary serializer or serialized as JSON.
        /// </summary>
        UserType = 126,

        /// <summary>
        /// A virtual type enum used as return value of <see cref="TypeEnumOrFixedSize.TypeEnum"/> for blittable types (fixed-length type with fixed layout).
        /// </summary>
        /// <remarks>
        ///
        /// ```
        /// 0                   1                   2                   3
        /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
        /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
        /// | Version+Flags |   FixedSize   |         Size as short         |
        /// +---------------------------------------------------------------+
        /// ```
        /// </remarks>
        FixedSize = TypeEnumOrFixedSize.MaxTypeEnum, // 127
    }
}
