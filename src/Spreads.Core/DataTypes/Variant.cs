using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads {


    // misery and pain ahead!

    // we need a convenient structure to work from code, not only store as bytes
    // we need it non-generic. 


    // TODO ensure alignment of uncompressed arrays for at least TypeEnum <= 10

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
        Object = 243, // run-time object, should serialize to Binary

        // Containers

        Array = 250,
        Map = 251, // could implement as two arrays
        Tuple

        //Category = 252, // just two arrays: Levels (their index is a value) and values


    }

    // TODO try to make the flags
    /// <summary>
    /// On-disk flags
    /// </summary>
    internal enum VersionAndFlags : byte {

    }

    /// <summary>
    /// On-disk header
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
    internal struct VariantHeader {
        
        // First 4 bytes are always the same
        internal const int VersionAndFlagsOffset = 0;
        [FieldOffset(VersionAndFlagsOffset)]
        private readonly VersionAndFlags _versionAndFlags;

        internal const int TypeEnumOffset = 1;
        [FieldOffset(TypeEnumOffset)]
        private readonly TypeEnum _typeEnum;

        internal const int TypeSizeOffset = 2;
        // If size if fixed then this should be positive
        [FieldOffset(TypeSizeOffset)]
        private readonly byte _typeSize;

        internal const int ElementTypeEnumOffset = 3;
        [FieldOffset(ElementTypeEnumOffset)]
        private readonly byte _elementTypeEnum;

    }


    // Runtime representation of Variant type

    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public unsafe partial struct Variant {
        // all data stored in place
        // it must have object be a BoxedTypeEnum
        [FieldOffset(0)]
        private fixed byte _data[16];

        // 
        [FieldOffset(0)]
        private readonly TypeEnum _typeEnum;        // byte
        [FieldOffset(1)]
        private readonly RuntimeTypeInfoFlags _flags;       // byte
        [FieldOffset(2)]
        private readonly byte _typeSize;            // byte
        [FieldOffset(3)]
        private readonly TypeEnum _subTypeEnum;            // byte

        [FieldOffset(4)]
        public readonly int _length;                 // int

        [FieldOffset(8)]
        internal readonly uint _offset;               // int

        // Object is null, we have 
        [FieldOffset(8)]
        internal readonly UIntPtr _pointer;           // long


        // When this is BoxedTypeEnum, _data field contains value
        // otherwise, _data contains TypeEnum at offset 0
        [FieldOffset(16)]
        private readonly object _object;


        public Variant(int value) {
            this = default(Variant);
            _length = value;
        }


        public TypeEnum TypeEnum
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return boxed.TypeEnum;
                }
                return _typeEnum;
            }
        }

        /// <summary>
        /// Number of values stored in Variant (1 for scalar values)
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return 1;
                }
                return _typeEnum == TypeEnum.Array ? _length : 1;
            }
        }


        /// <summary>
        /// Get value at index as variant.
        /// (could be copied if size LE 16 or returned as offset and length 1, this is implementation detail) 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public Variant Get(int index) // Same as Get<Variant>
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get value at index with type size, buffer size and other checks
        /// </summary>
        public T Get<T>(int index) {
            throw new NotImplementedException();
        }

        // get without any checks, must be called only when we know that all checks are OK
        internal T GetUnsafe<T>(int index) {
            throw new NotImplementedException();
        }

        //public static Variant Create<T>(T value)

        private T DataAs<T>() {
            fixed (void* ptr = _data) {
                return Unsafe.Read<T>(ptr);
            }
        }

        //private const sbyte DoubleCode = 14;

        //private void AssertType(sbyte code) {
        //    if (TypeCode != code && TypeCode != default(sbyte)) throw new InvalidCastException("Invalid cast");
        //}


        //public void Test() {
        //    var v = (Variant)123.0;
        //}
        //public static implicit operator Variant(double d) {
        //    return new Variant {
        //        Double = d
        //    };
        //}
        //public static implicit operator double(Variant d) {
        //    //d.AssertType(DoubleCode);
        //    return d.Double;
        //}
        //public static explicit operator Variant(double d) {
        //    return new Variant {
        //        Double = d
        //    };
        //}
        //public static explicit operator double (Variant d) {
        //    d.AssertType((0.0).GetTypeCode());
        //    return d.Double;
        //}



        private static readonly int[] ClrToSpreadsTypeCode = new int[18]
        {
            0, // 0
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
        };


        internal class BoxedTypeEnum {
            // TODO limit small (<16 bytes) known types to a small number 20-30
            private static readonly BoxedTypeEnum[] Cache = new BoxedTypeEnum[30];
            private BoxedTypeEnum(TypeEnum typeEnum) {
                TypeEnum = typeEnum;
            }
            public TypeEnum TypeEnum { get; }

            internal static BoxedTypeEnum Get(TypeEnum typeEnum) {
                var existing = Cache[(int)typeEnum];
                if (existing != null) return existing;
                var newBoxed = new BoxedTypeEnum(typeEnum);
                Cache[(int)typeEnum] = newBoxed;
                return newBoxed;
            }
        }


        [Flags]
        internal enum RuntimeTypeInfoFlags : byte {
            None = 0,
            IsFixedMemory = 1,
            IsFixedTypeSize = 2,
            IsCompressed = 4,
        }
    }
}
