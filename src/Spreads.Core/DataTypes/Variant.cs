// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes {

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

        internal Variant(object value) {
            this = Create(value);
        }

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
                return FromObject((object)value);
            }
            throw new NotImplementedException();
        }

        public static Variant FromObject(object value) {
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


        public static object ToObject(Variant value) {
            if (value.TypeEnum == TypeEnum.None) {
                return null;
            }
            // unwrap potentially boxed known types
            var typeEnum = value.TypeEnum;
            if ((int)typeEnum < KnownSmallTypesLimit) {
                switch (typeEnum) {
                    case TypeEnum.None:
                        throw new InvalidOperationException("TypeEnum.None is possible only for nulls");
                    case TypeEnum.Int8:
                        return value.Get<sbyte>();

                    case TypeEnum.Int16:
                        return value.Get<short>();

                    case TypeEnum.Int32:
                        return value.Get<int>();

                    case TypeEnum.Int64:
                        return value.Get<long>();

                    case TypeEnum.UInt8:
                        return value.Get<byte>();

                    case TypeEnum.UInt16:
                        return value.Get<ushort>();

                    case TypeEnum.UInt32:
                        return value.Get<uint>();

                    case TypeEnum.UInt64:
                        return value.Get<ulong>();

                    case TypeEnum.Float32:
                        return value.Get<float>();

                    case TypeEnum.Float64:
                        return value.Get<double>();

                    case TypeEnum.Decimal:
                        return value.Get<decimal>();

                    case TypeEnum.Price:
                        return value.Get<Price>();

                    case TypeEnum.Money:
                        return value.Get<Money>();

                    case TypeEnum.DateTime:
                        return value.Get<DateTime>();

                    case TypeEnum.Timestamp:
                        return value.Get<Timestamp>();

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

            // all other types are stored directly as objects
            if (value._object != null) return value._object;

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

        public bool IsFixedSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var te = TypeEnum;
                return (int)te < KnownSmallTypesLimit | te == TypeEnum.FixedBinary;
            }
        }

        public int ByteSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return KnownTypeSizes[(int)boxed.TypeEnum];
                }
                if (_header.TypeEnum == TypeEnum.FixedBinary) {
                    return _header.TypeSize;
                }
                return -1;
            }
        }

        public int ElementByteSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    return -1; // inlined scalap has no elements
                }
                if (_header.TypeEnum == TypeEnum.Array) {
                    return _header.TypeSize;
                }
                return -1;
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
                    throw new InvalidCastException("Variant type doesn't match typeof(T)");
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
                    throw new InvalidCastException("Variant type doesn't match typeof(T)");
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
                throw new NotImplementedException();
            }
            var boxed = _object as BoxedTypeEnum;
            if (boxed != null) {
                // Inline;
                var teOfT = VariantHelper<T>.TypeEnum;
                var te = this._boxedTypeEnum.TypeEnum;
                if (te != teOfT) {
                    throw new InvalidCastException("Variant type doesn't match typeof(T)");
                }
                fixed (void* ptr = _data) {
                    return new Span<T>(ptr, 1);
                }
            }
            // Object
            var typeEnum = _header.TypeEnum;
            if (typeEnum == TypeEnum.Array) {
                var elementEnum = _header.ElementTypeEnum;
                var castElementEnum = VariantHelper<T>.TypeEnum;
                if (elementEnum != castElementEnum) {
                    throw new InvalidCastException();
                }
                T[] array = (T[])_object;
                return new Span<T>(array, (int)_offset, _count);
            }
            throw new InvalidCastException();
        }

        /// <summary>
        ///
        /// </summary>
        public Variant this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) {
                    throw new ArgumentOutOfRangeException();
                }
                if (_object == null) {
                    // Pointer;
                    throw new NotImplementedException();
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    // count was 1 and if we here, index = 0
                    return this;
                }
                // Object
                var typeEnum = _header.TypeEnum;
                if (typeEnum == TypeEnum.Array) {
                    var elementEnum = _header.ElementTypeEnum;
                    Array array = (Array)_object;
                    var value = array.GetValue(index);
                    var v = Variant.Create(value);
                    Debug.Assert(elementEnum == v.ElementTypeEnum);
                    return v;
                }
                throw new InvalidCastException();
            }
            set
            {
                if ((uint)index >= (uint)Count) {
                    throw new ArgumentOutOfRangeException();
                }
                if (_object == null) {
                    // Pointer;
                    throw new NotImplementedException();
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null) {
                    if (boxed.TypeEnum != value.TypeEnum) {
                        throw new InvalidCastException();
                    }
                    // count was 1 and if we are here, index = 0
                    fixed (void* ptr = _data) {
                        *(decimal*)ptr = *(decimal*)value._data;
                    }
                    return;
                }
                // Object
                var typeEnum = _header.TypeEnum;
                if (typeEnum == TypeEnum.Array) {
                    if (_header.ElementTypeEnum != value.TypeEnum) {
                        throw new InvalidCastException($"{_header.ElementTypeEnum} - {value._header.TypeEnum}");
                    }
                    Array array = (Array)_object;
                    var val = ToObject(value);
                    array.SetValue(val, index);
                    return;
                }
                throw new InvalidCastException();
            }
        }

        /// <summary>
        /// Get value at index
        /// </summary>
        public T Get<T>(int index) {
            // TODO manual impl without span
            var span = Span<T>();
            return span[index];
        }

        /// <summary>
        /// Set value at index
        /// </summary>
        public void Set<T>(int index, T value) {
            var span = Span<T>();
            span[index] = value;
        }

        internal class BoxedTypeEnum {
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
