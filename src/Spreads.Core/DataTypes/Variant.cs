// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.DataTypes {
    // we need a convenient structure to work from code, not only store as bytes
    // we need it non-generic.

    // TODO ensure alignment of uncompressed arrays for at least TypeEnum <= 10

    /// <summary>
    /// Version and flags
    /// 0
    /// 0 1 2 3 4 5 6 7 8
    /// +-+-+-+-+-+-+-+-+
    /// |  Ver  | Flg |C|
    /// +---------------+
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 1)]
    public struct VersionAndFlags {
        internal const int VersionBitsOffset = 4;
        internal const int CompressedBitOffset = 0;
        // three more flags left

        private byte _value;

        public byte Version
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (byte)(_value >> VersionBitsOffset); }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _value = (byte)(_value | (value << VersionBitsOffset)); }
        }

        public bool IsCompressed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_value & (1 << CompressedBitOffset)) > 0; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _value = (byte)(_value & ((value ? 1 : 0) << CompressedBitOffset)); }
        }
    }

    /// <summary>
    /// Variant header
    /// 0                   1                   2                   3
    /// 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    /// +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    /// |  Ver  | Flg |C|    TypeEnum   |  TypeSize     | SubTypeEnum   |
    /// +---------------------------------------------------------------+
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 4)]
    public struct VariantHeader {

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
    }

    // Runtime representation of Variant type

    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public unsafe partial struct Variant {

        /// <summary>
        /// Maximum number of types with size LE than 16 bytes that are explicitly defined in TypeEnum
        /// </summary>
        internal const int KnownSmallTypesLimit = 64;

        public enum VariantLayout {

            /// <summary>
            /// Single data point is stored inline in the internal fixed buffer.
            /// Object field is set to a special statically cached object containing metadata (BoxedTypeEnum).
            /// This layout is used for TypeEnum codes that are less than KnownSmallTypesLimit
            /// </summary>
            Inline = 0,

            /// <summary>
            /// Object is not null and is not BoxedTypeEnum.
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
        private VariantHeader _header;        // int

        // Number of elements in array
        [FieldOffset(4)]
        public int _count;                 // int

        // TODO for pointers, this should be length or the memory
        // or, any var len variant should have length as the first 4 bytes

        // Object-only layout, optional offset used only for arrays
        [FieldOffset(8)]
        internal ulong _offset;               // long

        // Pointer layout, object is null
        [FieldOffset(8)]
        internal UIntPtr _pointer;           // long

        // When this is BoxedTypeEnum, _data field contains value
        // otherwise, _data contains VariantHeader at offset 0
        [FieldOffset(16)]
        internal object _object;

        [FieldOffset(16)]
        internal BoxedTypeEnum _boxedTypeEnum;

        //internal Variant(object obj) {
        //    this = default(Variant);
        //    var ty = obj.GetType();
        //    _header.TypeEnum = VariantHelper.GetTypeEnum(ty);

        //    if (_header.TypeEnum == TypeEnum.Array) {
        //        _header.ElementTypeEnum = VariantHelper.GetTypeEnum(ty.GetElementType());
        //    }
        //    // TODO OwnedMemory is alway of type `byte`
        //    _object = obj;
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T value = default(T)) {
            var boxedTypeEnum = BoxedTypeEnum<T>.CachedBoxedTypeEnum;
            var typeEnum = boxedTypeEnum.TypeEnum;
            if ((int)typeEnum < KnownSmallTypesLimit) {
                // inline layout
                var variant = new Variant { _boxedTypeEnum = boxedTypeEnum };
                Unsafe.Write(variant._data, value);
                return variant;
            }
            return CreateSlow(value, typeEnum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create(object value) {
            dynamic d = value;
            return Create(d);
        }

        //[MethodImpl(MethodImplOptions.NoInlining)]
        private static Variant CreateSlow<T>(T value, TypeEnum typeEnum) {
            if (typeEnum == TypeEnum.Variant) return (Variant)(object)(value);
            if (typeEnum == TypeEnum.Object) {
                return CreateFromObject((object)value);
            }
            throw new NotImplementedException();
        }

        private static Variant CreateFromObject(object value) {
            if (value == null) {
                return new Variant { _header = { TypeEnum = TypeEnum.None } };
            }
            // unwrap potentially boxed known types
            var objTypeEnum = VariantHelper.GetTypeEnum(value.GetType());
            if ((int)objTypeEnum < KnownSmallTypesLimit) {
                switch (objTypeEnum) {
                    case TypeEnum.None:
                        throw new InvalidOperationException("TypeEnum.None is possible only for nulls");
                    case TypeEnum.Int8:
                        return Create((sbyte)value);

                    case TypeEnum.Int16:
                        return Create((short)value);

                    case TypeEnum.Int32:
                        return Create((int)value);

                    case TypeEnum.Int64:
                        return Create((long)value);

                    case TypeEnum.UInt8:
                        return Create((byte)value);

                    case TypeEnum.UInt16:
                        return Create((ushort)value);

                    case TypeEnum.UInt32:
                        return Create((uint)value);

                    case TypeEnum.UInt64:
                        return Create((ulong)value);

                    case TypeEnum.Float32:
                        return Create((float)value);

                    case TypeEnum.Float64:
                        return Create((double)value);

                    case TypeEnum.Decimal:
                        return Create((decimal)value);

                    case TypeEnum.Price:
                        return Create((Price)value);

                    case TypeEnum.Money:
                        return Create((Money)value);

                    case TypeEnum.DateTime:
                        return Create((DateTime)value);

                    case TypeEnum.Timestamp:
                        return Create((Timestamp)value);

                    case TypeEnum.Date:
                        throw new NotImplementedException();
                    case TypeEnum.Time:
                        throw new NotImplementedException();
                    case TypeEnum.Complex32:
                        throw new NotImplementedException();
                    case TypeEnum.Complex64:
                        throw new NotImplementedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (objTypeEnum == TypeEnum.Array) {
                Environment.FailFast("Array shoud have been dispatched via dynamic in the untyped Create method");
            }

            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T[] array) {
            return Create<T>(array, 0, array.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T[] array, int offset, int length) {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var v = new Variant {
                _object = array,
                _count = length,
                _offset = (ulong)offset,
                _header = new VariantHeader {
                    TypeEnum = TypeEnum.Array,
                    ElementTypeEnum = VariantHelper<T>.TypeEnum,
#pragma warning disable 618
                    TypeSize = checked((byte)TypeHelper<T>.Size) // TODO review
#pragma warning restore 618
                }
            };
            return v;
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
                return _header.TypeEnum;
            }
        }

        public int ElementSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return -1;
                }
                return _header.TypeSize;
            }
        }

        public TypeEnum ElementTypeEnum
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return TypeEnum.None;
                }
                return _header.ElementTypeEnum;
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
                return _header.TypeEnum == TypeEnum.Array ? _count : 1;
            }
        }


        internal VariantLayout Layout
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>() {
            var teOfT = VariantHelper<T>.TypeEnum;
            if ((int)teOfT < KnownSmallTypesLimit) {
                var te = this._boxedTypeEnum.TypeEnum;
                if (te != teOfT) {
                    throw new InvalidOperationException("Variant type doesn't match typeof(T)");
                }
                fixed (void* ptr = _data) {
                    return Unsafe.Read<T>(ptr);
                }
            }
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(T value) {
            var teOfT = VariantHelper<T>.TypeEnum;
            if ((int)teOfT < KnownSmallTypesLimit) {
                var te = this._boxedTypeEnum.TypeEnum;
                if (te != teOfT) {
                    throw new InvalidOperationException("Variant type doesn't match typeof(T)");
                }
                fixed (void* ptr = _data) {
                    Unsafe.Write(ptr, value);
                    return;
                }
            }
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read a value T directly from the internal fixed 16-bytes buffer
        /// without any checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnsafeGetInilned<T>() {
            Debug.Assert(Layout == VariantLayout.Inline);
            fixed (void* ptr = _data) {
                return Unsafe.Read<T>(ptr);
            }
        }

        /// <summary>
        /// Write a value T directly to the internal fixed 16-bytes buffer
        /// without any checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeSetInlined<T>(T value) {
            Debug.Assert(Layout == VariantLayout.Inline);
            fixed (void* ptr = _data) {
                Unsafe.Write(ptr, value);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Span<T>() {
            if (_object == null) {
                // Pointer;
            }
            var boxed = _object as BoxedTypeEnum;
            if (boxed != null) {
                // Inline;
                var teOfT = VariantHelper<T>.TypeEnum;
                if ((int)teOfT < KnownSmallTypesLimit) {
                    var te = this._boxedTypeEnum.TypeEnum;
                    if (te != teOfT) {
                        throw new InvalidOperationException("Variant type doesn't match typeof(T)");
                    }
                    fixed (void* ptr = _data) {
                        return new Span<T>(ptr, 1);
                    }
                }
            } else {
                // Object
                var typeEnum = _header.TypeEnum;
                if (typeEnum == TypeEnum.Array)
                {
                    T[] array = (T[]) _object;
                    return new Span<T>(array, (int)_offset, _count);
                }
            }

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


        internal class BoxedTypeEnum {

            // TODO limit small (<16 bytes) known types to a small number 20-30
            private static readonly BoxedTypeEnum[] Cache = new BoxedTypeEnum[KnownSmallTypesLimit];

            internal BoxedTypeEnum(TypeEnum typeEnum) {
                TypeEnum = typeEnum;
            }

            public TypeEnum TypeEnum { get; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static BoxedTypeEnum Get(TypeEnum typeEnum) {
                var existing = Cache[(int)typeEnum];
                if (existing != null) return existing;
                var newBoxed = new BoxedTypeEnum(typeEnum);
                Cache[(int)typeEnum] = newBoxed;
                return newBoxed;
            }
        }

        internal class BoxedTypeEnum<T> {
            public static BoxedTypeEnum CachedBoxedTypeEnum = new BoxedTypeEnum(VariantHelper<T>.TypeEnum);
        }
    }
}
