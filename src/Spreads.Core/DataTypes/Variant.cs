using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads {


    // misery and pain ahead!

    // we need a convenient structure to work from code, not only store as bytes
    // we need it non-generic. 


    // TODO ensure alignment of uncompressed arrays for at least TypeEnum <= 10


    // TODO try to make the flags
    /// <summary>
    /// On-disk flags
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    internal struct VersionAndFlags {
        internal const int VersionBitsOffset = 4;
        internal const int CompressedBitOffset = 0;
        // three more flags left

        private byte _value;

        public byte Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (byte)(_value >> VersionBitsOffset); }
        }

        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_value & (1 << CompressedBitOffset)) > 0; }
        }
    }

    /// <summary>
    /// On-disk header
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
    internal struct VariantHeader {

        // First 4 bytes are always the same
        internal const int VersionAndFlagsOffset = 0;
        [FieldOffset(VersionAndFlagsOffset)]
        internal VersionAndFlags _versionAndFlags;

        internal const int TypeEnumOffset = 1;
        [FieldOffset(TypeEnumOffset)]
        internal TypeEnum _typeEnum;

        internal const int TypeSizeOffset = 2;
        // If size if fixed then this should be positive
        [FieldOffset(TypeSizeOffset)]
        internal byte _typeSize;

        internal const int ElementTypeEnumOffset = 3;
        [FieldOffset(ElementTypeEnumOffset)]
        internal TypeEnum _elementTypeEnum;
    }




    // Runtime representation of Variant type

    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public unsafe partial struct Variant {
        public enum VariantLayout {
            /// <summary>
            /// Single data point is stored inline in the internal data field.
            /// Object field is set to special statically cached objects containing metadata.
            /// </summary>
            Inline = 0,
            /// <summary>
            /// Object is not null and is not boxed TypeEnum.
            /// </summary>
            Object = 1,
            /// <summary>
            /// Object is null.
            /// </summary>
            Pointer = 2
        }

        // Inline layout, object is a BoxedTypeEnum
        [FieldOffset(0)]
        private fixed byte _data[16];

        // Object and pointer layout
        [FieldOffset(0)]
        private VariantHeader _header;        // byte
        // Number of elements in array
        [FieldOffset(4)]
        public readonly int _count;                 // int
        // TODO for pointers, this should be length or the memory
        // or, any var len variant should have length as the first 4 bytes

        // Object-only layout, optional offset used only for arrays
        [FieldOffset(8)]
        internal readonly ulong _offset;               // long


        // Pointer layout, object is null
        [FieldOffset(8)]
        internal readonly UIntPtr _pointer;           // long


        // When this is BoxedTypeEnum, _data field contains value
        // otherwise, _data contains VariantHeader at offset 0
        [FieldOffset(16)]
        private readonly object _object;



        internal Variant(object obj) {
            this = default(Variant);
            var ty = obj.GetType();
            _header._typeEnum = GetTypeCode(ty);

            if (obj.GetType().IsArray) {
                _header._elementTypeEnum = GetTypeCode(ty.GetElementType());
            }
            _object = obj;
        }

        public VariantLayout Layout
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_object == null) {
                    return VariantLayout.Pointer;
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return VariantLayout.Inline;
                }
                return VariantLayout.Object;
            }
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
                return _header._typeEnum;
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
                return _header._typeEnum == TypeEnum.Array ? _count : 1;
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
    }
}
