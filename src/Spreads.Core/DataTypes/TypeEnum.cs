// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.DataTypes
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
    /// </summary>
    public enum TypeEnum : byte
    {
        // NB Any changes to TypeEnum must be reflected in the know type sizes array at the bottom

        // NB Enum values themselves should never be used directly, only the array below should depend on them
        // However, for non-.NET clients they should stabilize rather sooner than later
        
        None = 0,

        // Fixed-length known types - their length is defined by code

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

        [Obsolete("Free to redefine, was never used")]
        Unused16 = 16,

        [Obsolete("Free to redefine, was never used")]
        Unused17 = 17,

        [Obsolete("Free to redefine, was never used")]
        Unused18 = 18,

        [Obsolete("Free to redefine, was never used")]
        Unused19 = 19,

        Bool = 20,

        Id = 21,

        Symbol = 22, // we have several implementations, but all fixed. example when same fixed TypeEnum has different sizes. TODO Should we support this?
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
        Json = 65,
        Binary = 67,

        Variant = 68, // for sub-type in containers, must throw for scalars

        [Obsolete("None + KnownTypeId should work. If KTI is 0 then payload is just opaque bytes")]
        Object = 69, // custom object, TODO the type must have a KnownType attribute

        Array = 70,
        Matrix = 71, // same as array must have a subtype defined
        Table = 72,  // matrix of variants,

        ArrayBasedMap = 73,

        TaggedKeyValue = 74,
        
        // Up to 127 are reserved for Spreads hard-coded types

        
    }

    public partial struct Variant
    {
        #region Known Type Sizes

        // TODO use Offheap buffer: byte[] has object header so this doesn't fit one cache line. Need to ensure alignment of offheap allocatoin as well

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

            // Unused
            0, // 16
            0,

            0,
            0, // 19

            
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