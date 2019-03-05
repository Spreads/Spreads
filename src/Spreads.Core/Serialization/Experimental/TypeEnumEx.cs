// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal.Experimental;
using System;

namespace Spreads.Serialization.Experimental
{
    /// <summary>
    /// Known types and containers enumeration.
    /// </summary>
    /// <remarks>
    /// The goal of this enumeration is to have a unique small ids for frequently used
    /// types, provision ids for likely future types and set ids to main containers.
    /// Types that cannot be described with this simple enumeration must have user-defined
    /// <see cref="IBinarySerializer{T}"/> or use a schema (TODO)
    /// <para />
    ///
    /// Integer types are always serialized as little-endian.
    /// Big-endian is completely not and won't be supported in foreseeable future.
    /// </remarks>
    public enum TypeEnumEx : byte
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

        // Non-container values are schema hints, payload is just a byte string.

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
        /// A fixed-length collection with up to 127 elements of the same type.
        /// Element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and count is stored in  <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// This covers all same fixed-size type tuples from 1 to 127.
        /// </summary>
        FixedArrayT = 70, // Note: FixedArray without T is Schema.

        // TupleNT are aliases to FixedArrayT for sizes 1-5.

        /// <summary>
        /// A tuple with two elements of same type. Element type are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// </summary>
        Tuple2T = 71,

        /// <summary>
        /// A tuple with three elements of same type. Element type are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// </summary>
        Tuple3T = 72,

        /// <summary>
        /// A tuple with four elements of same type. Element type are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// </summary>
        Tuple4T = 73,

        /// <summary>
        /// A tuple with five elements of same type. Element type are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// </summary>
        Tuple5T = 74,

        /// <summary>
        /// A tuple with five elements of same type. Element type are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// </summary>
        Tuple6T = 75,

        /// <summary>
        /// A tuple with two elements of different types. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots.
        /// </summary>
        Tuple2 = 76,

        /// <summary>
        /// A tuple with two elements of different types prefixed with <see cref="byte"/> tag. Element types are stored
        /// in <see cref="DataTypeHeaderEx.TEOFS1"/> and <see cref="DataTypeHeaderEx.TEOFS2"/>
        /// slots.
        /// </summary>
        Tuple2Tag = 77, // If we need a tag with a different type use Tuple3

        Tuple2Version = 78,

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
        /// A variable-length collection of elements of the same type. Element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/>.
        /// <see cref="DataTypeHeaderEx.TEOFS2"/> slot is used for a single subtype
        /// of array element, e.g. for jugged array.
        /// Always followed by payload length in bytes as <see cref="int"/>. If length is negative
        /// then element type must be fixed-size and the payload is shuffled.
        /// </summary>
        Array = 80,

        // TODO No sign-flip BS, use 2-bit flags and limit array length to 1GB. Highest bit is shuffled, next one is deltas.
        // For some types delta is highly efficient because some fields do not change and we have zeros.

        /// <summary>
        /// Depth is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and type in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// </summary>
        JaggedArray = 81,

        /// <summary>
        /// Inner array element type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and it's size in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// This covers array of all same fixed-size type tuples from 1 to 127.
        /// </summary>
        ArrayOfFixedArraysT = 82,

        // TODO number of dimensions is runtime parameter or not? Should it be in payload header?
        /// <summary>
        /// Multidimensional array with up to 127 dimensions. Value type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and number of dimensions
        /// in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// E.g. <see cref="Matrix{T}"/> is NDArray with 2 dimensions.
        /// </summary>
        NDArray = 83, // same as array must have a subtype defined

        /// <summary>
        /// Table is a frame with <see cref="string"/> row and column keys and <see cref="Variant"/> data type.
        /// </summary>
        Table = 84,

        /// <summary>
        /// <see cref="Array"/> of <see cref="Tuple2"/> with unique keys. Key type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/>
        /// and value type is stored in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// </summary>
        Map = 85, // Need this to fit type info in 3 TEOFS as in DataTypeHeader and avoid TEOFS3 in VariantHeader.

        /// <summary>
        /// Two-array map (dictionary).
        /// Key type is stored in <see cref="DataTypeHeaderEx.TEOFS1"/> and value type in <see cref="DataTypeHeaderEx.TEOFS2"/>.
        /// </summary>
        Series = 86,

        Frame = 87,

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
        /// Stores schema id in <see cref="VariantHeader"/> two subtype slots as <see cref="ushort"/>.
        /// </summary>
        Schema = 124,

        /// <summary>
        /// A custom user type that has a binary serializer.
        /// </summary>
        UserKnownType = 125,

        /// <summary>
        /// A custom user type without binary serializer. Serialized data is JSON.
        /// </summary>
        UserType = 126,

        /// <summary>
        /// A virtual type enum used as return value of <see cref="TypeEnumOrFixedSize.TypeEnum"/> for blittable types (fixed-length type with fixed layout).
        /// </summary>
        FixedSize = TypeEnumOrFixedSize.MaxTypeEnum, // 127
    }
}
