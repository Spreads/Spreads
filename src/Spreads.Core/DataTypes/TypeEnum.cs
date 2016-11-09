// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads.DataTypes {
    // NB Any changes to TypeEnum must be reflected in te know type sizes array at the bottom

    /// <summary>
    /// Known types and containers enumeration.
    /// </summary>
    public enum TypeEnum : byte {
        None = 0,

        // Fixed-length known types - their length is defined by code

        Bool = 197,

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

        // TODO chck if there is IEEE standard for comlex
        // TODO rename to Tuple
        /// <summary>
        /// Real + imaginary Float32 values (total size 8 bytes)
        /// </summary>
        Complex32 = 18,

        /// <summary>
        /// Real + imaginary Float64 values (total size 16 bytes)
        /// </summary>
        Complex64 = 19,

        // We could define up to 200 known fixed-size types,
        // e.g. Price, Tick, Point2Df (float), Point3Dd(double)
        //Symbol8 = 20,
        //Symbol16 = 21,
        //Symbol32 = 22,
        //Symbol64 = 23,
        //Symbol128 = 24,

        // Comparison [(byte)(TypeEnum) < 198 = true] means known fixed type

        /// <summary>
        /// Used for blittable types (fixed-length type with fixed layout)
        /// </summary>
        FixedBinary = 198,

        /// <summary>
        /// Array with fixed number of elements (space is reserved even if it is not filled)
        /// </summary>
        //FixedArray = 199, // this could be either fixed if sub-type is fixed or variable

        // Variable size types

        String = 200,
        Binary = 201,

        Variant = 242, // for sub-type in containers, must throw for scalars
        Object = 243, // custom object, should serialize to Binary

        // Containers

        Array = 250,
        //Table = 252,
        //Tuple = 252

        //Category = 252, // just two arrays: Levels (their index is a value) and values
    }

    public partial struct Variant {

        #region Known Type Sizes

        private static readonly byte[] KnownTypeSizes = new byte[198]
        {
            // Unknown
            0, // 0
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
            4,
            8, // 0

            16, // Decimal
            8,  // Price
            0, // TODO Money not implemented

            // DateTime
            8,
            8, // 5
            4,
            4,

            // Complex
            8,
            16,

            // Symbols
            0, // 20 TODO Symbol8, 32-128
            0, //
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            1, // 197 - bool
        };

        #endregion Known Type Sizes
    }
}