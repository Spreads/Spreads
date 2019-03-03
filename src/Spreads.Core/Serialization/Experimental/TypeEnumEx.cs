// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal.Experimental;
using System;

namespace Spreads.Serialization.Experimental
{
    // TODO Values < 64 used for Variant with 16 bytes limit, but Variant is not used currently much
    // and there is not so many 16-byte predefined types. Limit 16-byte types to <32, from 32 to 63 could
    // be types with size up to 255 bytes. Above 64 we have a lot of space to define any containers and
    // other var-size types.
    // 64 is for user-defined blittable types, they could have subtype with known type id (WIP, TODO)
    // Should limit Spreads type to < 127, the other half should go to DS types.
    // None type is opaque var-sized, it could have known type at app level.

    /// <summary>
    /// Known types and containers enumeration.
    /// Integer types are always serialized as little-endian.
    /// Big-endian is completely not and won't be supported in foreseeable future.
    /// </summary>
    public enum TypeEnumEx : byte
    {
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
        // Our WIP schema could be tightly packed, but just ints is faster and schema is static.

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
        DecimalDotNet = 18,

        /// <summary>
        /// See <see cref="SmallDecimal"/>.
        /// </summary>
        SmallDecimal = 19,

        /// <summary>
        /// See <see cref="Money"/>.
        /// </summary>
        Money = 20,

        /// <summary>
        /// See <see cref="bool"/>.
        /// </summary>
        Bool = 21,

        /// <summary>
        /// See <see cref="char"/>
        /// </summary>
        Utf16Char = 22,

        /// <summary>
        /// See <see cref="UUID"/>. Could store <see cref="Guid"/> as well, but there is no restrictions on format.
        /// </summary>
        UUID = 23,

        Symbol = 24,

        /// <summary>
        /// <see cref="DateTime"/> UTC ticks (100ns intervals since zero) as UInt64
        /// </summary>
        DateTime = 25,

        /// <summary>
        /// <see cref="Timestamp"/> as nanoseconds since Unix epoch as UInt64
        /// </summary>
        Timestamp = 26,

        // ----------------------------------------------------------------
        // Comparison [(byte)(TypeEnum) < 64 = true] means known fixed type
        // ----------------------------------------------------------------

        #endregion Fixed-length known scalar types

        #region Variable size known types

        // Non-container values are schema hints, payload is just a byte string.

        /// <summary>
        /// Opaque binary string.
        /// </summary>
        Binary = 64,

        /// <summary>
        /// A Utf8 string prefixed by <see cref="int"/> length.
        /// </summary>
        Utf8String = 65,

        /// <summary>
        /// A Utf16 <see cref="string"/> prefixed by <see cref="int"/> length.
        /// </summary>
        Utf16String = 66,

        /// <summary>
        /// Utf8 JSON prefixed by <see cref="int"/> length.
        /// </summary>
        Json = 67,

        #endregion Variable size known types

        #region Tuple-like structures that do not need TEOFS3

        /// <summary>
        /// Fixed-length array with up to 127 elements of the same type.
        /// Element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and count is stored in  <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// This covers all same fixed-size type tuples from 1 to 127.
        /// </summary>
        FixedArrayT = 70, // Note: FixedArray without T is Schema.

        /// <summary>
        /// A tuple with two elements of different types. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots.
        /// </summary>
        Tuple2 = 71,

        /// <summary>
        /// A tuple with two elements of different types prefixed with <see cref="byte"/> tag. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots.
        /// </summary>
        Tuple2Tag = 72, // If we need a tag with a different type use Tuple3

        Tuple2Version = 73,

        // TODO return here when start working with frames.

        /// <summary>
        /// Row key type in TEOFS1, value type in TEOFS2. Always followed by <see cref="int"/> column index.
        /// Used for <see cref="Matrix{T}"/> and <see cref="Frame{TRow,TCol}"/> in-place value updates.
        /// </summary>
        [Obsolete("Not used actually. Just an idea how to serialize updates.")]
        KeyIndexValue = 74,

        #endregion Tuple-like structures that do not need TEOFS3

        #region Containers with 1-2 subtypes

        /// <summary>
        /// Element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// <see cref="DataTypeHeaderEx.TEOFS2"/> slot is used for a single subtype
        /// of array element, e.g. for jugged array.
        /// Always followed by payload length in bytes as <see cref="int"/>. If length is negative
        /// then element type must be fixed-size and the payload is shuffled.
        /// </summary>
        Array = 80,

        // TODO No sign-flip BS, use 2-bit flags and limit array length to 1GB. Highest bit is shuffled, next one is deltas.
        // For some types delta is highly efficient because some fields do not change and we have zeros.

        /// <summary>
        /// Inner array element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and it's size in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// This covers array of all same fixed-size type tuples from 1 to 127.
        /// </summary>
        ArrayOfFixedArraysT = 81,

        /// <summary>
        /// See <see cref="Matrix{T}"/>.
        /// </summary>
        MatrixT = 82, // same as array must have a subtype defined

        /// <summary>
        /// Table is a frame with <see cref="string"/> row and column keys and <see cref="Variant"/> data type.
        /// </summary>
        Table = 83,

        /// <summary>
        /// Key type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and value type in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// </summary>
        Series = 84,

        //VectorStorage = 73,

        //TaggedKeyValue = 74,

        #endregion Containers with 1-2 subtypes

        #region Types that require 3 subtypes

        /// <summary>
        /// A tuple with three elements of different types. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots. Always followed by <see cref="VariantHeader.TEOFS3"/> for the third element type.
        /// </summary>
        Tuple3 = 90,

        /// <summary>
        /// A tuple with three elements of different types prefixed with <see cref="byte"/> tag. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots. Always followed by <see cref="VariantHeader.TEOFS3"/> for the third element type.
        /// </summary>
        Tuple3Tag = 91, // If we need a tag with a different type use Schema

        /// <summary>
        /// See <see cref="Frame{TRow,TCol,T}"/>.
        /// Always followed by <see cref="VariantHeader.TEOFS3"/> for the value type.
        /// </summary>
        FrameT = 99,

        #endregion Types that require 3 subtypes

        /// <summary>
        /// See <see cref="Variant"/>. Has own <see cref="DataTypeHeader"/> before payload.
        /// </summary>
        Variant = 120,

        /// <summary>
        /// Stores schema id in <see cref="DataTypeHeader"/> two subtype slots as <see cref="ushort"/>.
        /// </summary>
        Schema = 125,

        /// <summary>
        /// Stores user-provided known type id in <see cref="DataTypeHeader"/> two subtype slots as <see cref="ushort"/>.
        /// </summary>
        UserKnownType = 126,

        /// <summary>
        /// A virtual type enum used as return value of <see cref="TypeEnumOrFixedSize.TypeEnum"/> for blittable types (fixed-length type with fixed layout).
        /// </summary>
        FixedBinary = TypeEnumOrFixedSize.MaxTypeEnum, // 127
    }
}
