// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.DataTypes
{
    /// <summary>
    /// Known types and containers enumeration.
    /// </summary>
    public enum TypeEnum : byte
    {
        // NB Any changes to TypeEnum must be reflected in the know type sizes array at the bottom

        // NB Enum values themselves should never be used directly, only the array below should depend on them
        // However, for non-.NET clients they should stabilize rather sooner than later

        // Fixed-length known types - their length is defined by code
        None = 0,

        Int8 = 1,
        Int16 = 2,
        Int32 = 3,
        Int64 = 4,

        UInt8 = 5,
        UInt16 = 6,
        UInt32 = 7,
        UInt64 = 8,

        Float32 = 9,
        Float64 = 10,

        Decimal = 11,
        Price = 12,
        Money = 13,

        // TODO handling of DT.Kind should be in serializer settings later, by default we fail on any non UTC in serializer
        // if we need a time zone, we could add symbol
        // There could be a special case of array with TZ, but this could be easily achieved without built-in functionality

        /// <summary>
        /// DatetTime UTC ticks (100ns intervals since zero) as UInt64
        /// </summary>
        DateTime = 14,

        /// <summary>
        /// Nanoseconds since Unix epoch as UInt64
        /// </summary>
        Timestamp = 15,

        // TODO Need strong definition of what it is,
        // otherwise for app-specific definition one could use just Int family
        Date = 16,

        Time = 17,

        /// <summary>
        /// Real + imaginary Float32 values (total size 8 bytes)
        /// </summary>
        Complex32 = 18,

        /// <summary>
        /// Real + imaginary Float64 values (total size 16 bytes)
        /// </summary>
        Complex64 = 19,

        Bool = 20,

        Id = 21,
        Symbol = 22,
        // ReSharper disable once InconsistentNaming
        UUID = 23,

        Int128 = 24,
        UInt128 = 25,
        ErrorCode = 63,

        // ----------------------------------------------------------------
        // Comparison [(byte)(TypeEnum) < 64 = true] means known fixed type
        // ----------------------------------------------------------------

        /// <summary>
        /// Used for blittable types (fixed-length type with fixed layout)
        /// </summary>
        FixedBinary = DataTypes.Variant.KnownSmallTypesLimit, // 64

        // Variable size types
        
        String = 66,
        Binary = 67,

        Variant = 68, // for sub-type in containers, must throw for scalars

        Object = 69, // custom object, TODO the type must have a KnownType attribute

        Array = 70,
        Matrix = 71, // same as array must have a subtype defined
        Table = 72,  // matrix of variants,

        ArrayBasedMap = 73,

        TaggedKeyValue = 74,

        // NB For opaque types such as JSON/FlatBuffers/Thrift the value of TypeEnum is mostly to dispatch
        // a value to correct second(app)-level deserializer. E.g. we have subtype slot in 
        // DataType header that could be used to store additional information on what application type is 
        // encoded using the given serialization scheme (if the payload itself does not contain such info).

        Json = 100,
        JsonDeflate = 101,
        JsonBrotli = 102,
        FlatBuffers = 103,
        Thrift = 104,
        Sbe = 105,

        // Protocol
        EchoMessage = 110,
        RequestStreamInfoMessage = 111,
        ResponseStreamInfoMessage = 112,
        RequestAppendDataToStreamMessage = 113,
        ResponseAppendDataToStreamMessage = 114,

        // Up to 127 are reserved for something hard-coded

        // TODO(?) Reserve 128+ types as App-specific known ones that could be registered (e.g. via IBinaryConverter or separate attribute)
        // 128 is a big number for a single app
    }

    public partial struct Variant
    {
        #region Known Type Sizes

        // ReSharper disable once RedundantExplicitArraySize
        internal static readonly byte[] KnownTypeSizes = new byte[KnownSmallTypesLimit]
        {
            // Unknown
            0, // None
            // Int
            1,
            2,
            4,
            8,
            1, // 5
            2,
            4,
            8,
            // Float
            4, // double
            8, // single - 10

            16, // Decimal
            8,  // Price
            16, // Money

            
            8, // DateTime
            8, // 15 - Timestamp
            4,
            4,

            // Complex
            8,
            16,

            // Symbols
            1, // 20 - Bool
            4, // 21 - Id
            16, // 22 - Symbol
            16, // 23 - UUID
            16, // 24 - Int128
            16, // 25 - UInt128
            0,
            0,
            0,
            0,
            0, // 30
            0,
            0,
            0,
            0,
            0, // 35
            0,
            0,
            0,
            0,
            0, // 40
            0,
            0,
            0,
            0,
            0, // 45
            0,
            0,
            0,
            0,
            0, // 50
            0,
            0,
            0,
            0,
            0, // 55
            0,
            0,
            0,
            0,
            0, // 60
            0,
            0,
            8
        };

        #endregion Known Type Sizes
    }
}