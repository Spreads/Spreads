namespace Spreads
{
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
        FixedArray = 199, // this could be either fixed if sub-type is fixed or variable

        // Variable size types

        String = 200,
        Binary = 201,


        Variant = 242, // for sub-type in containers, must throw for scalars
        Object = 243, // custom object, should serialize to Binary

        // Containers

        Array = 250,
        Map = 251, // could implement as two arrays
        Table = 252, 
        //Tuple = 252

        //Category = 252, // just two arrays: Levels (their index is a value) and values


    }
}