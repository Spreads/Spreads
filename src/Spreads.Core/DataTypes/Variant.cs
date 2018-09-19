// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes
{
    // Runtime representation of Variant type

    [StructLayout(LayoutKind.Explicit, Pack = 4)]
    public unsafe partial struct Variant : IEquatable<Variant>
    {
        [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 16)]
        private struct Buffer16
        {
            private long l1;
            private long l2;
        }

        // TODO Structural equality

        /// <summary>
        /// Maximum number of types with size LE than 16 bytes that are explicitly defined in TypeEnum
        /// </summary>
        internal const int KnownSmallTypesLimit = 64;

        public enum VariantLayout
        {
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
        private Buffer16 _data;

        // Object and pointer layout
        [FieldOffset(0)]
        private DataTypeHeader _header;        // int

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
        // otherwise, _data contains DataTypeHeader at offset 0
        [FieldOffset(16)]
        internal object _object;

        [FieldOffset(16)]
        internal BoxedTypeEnum _boxedTypeEnum;

        internal Variant(object value)
        {
            this = Create(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T value = default(T))
        {
            //Console.WriteLine("Dispatched as scalar");
            if (VariantHelper<T>.IsInline)
            {
                // inline layout
                var variant = new Variant { _boxedTypeEnum = BoxedTypeEnum<T>.CachedBoxedTypeEnum };
                Unsafe.Write(Unsafe.AsPointer(ref variant._data), value);
                return variant;
            }
            return CreateSlow(value, VariantHelper<T>.TypeEnum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create(object value)
        {
            dynamic d = value;
            return Create(d);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Variant CreateSlow<T>(T value, TypeEnum typeEnum)
        {
            if (typeEnum == TypeEnum.Variant) return (Variant)(object)(value);
            if (typeEnum == TypeEnum.String)
            {
                return Create((string)(object)(value));
            }
            if (typeEnum == TypeEnum.Object || typeEnum == TypeEnum.Matrix || typeEnum == TypeEnum.Table)
            {
                return FromObject(value);
            }
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant FromObject(object value)
        {
            if (value == null)
            {
                return new Variant { _header = { TypeEnum = TypeEnum.None } };
            }
            // unwrap potentially boxed known types
            var objTypeEnum = VariantHelper.GetTypeEnum(value.GetType());
            if ((int)objTypeEnum < KnownSmallTypesLimit)
            {
                switch (objTypeEnum)
                {
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
                        throw new NotImplementedException();
                    //return Create((Money)value);

                    case TypeEnum.DateTime:
                        return Create((DateTime)value);

                    case TypeEnum.Timestamp:
                        return Create((Timestamp)value);

                    case TypeEnum.Bool:
                        return Create((bool)value);

                    case TypeEnum.ErrorCode:
                        return Create((ErrorCode)value);

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (objTypeEnum == TypeEnum.Array || objTypeEnum == TypeEnum.String)
            {
                return Create(value);
                //Environment.FailFast("Array shoud have been dispatched via dynamic in the untyped Create method");
            }

            if (objTypeEnum == TypeEnum.Matrix)
            {
                var elTy = value.GetType().GetElementType();
                var subTypeEnum = VariantHelper.GetTypeEnum(elTy);
                var v = new Variant
                {
                    _object = value,
                    _header = new DataTypeHeader
                    {
                        TypeEnum = TypeEnum.Matrix,
                        ElementTypeEnum = subTypeEnum
                    }
                };
                return v;
            }

            if (objTypeEnum == TypeEnum.Table)
            {
                var subTypeEnum = TypeEnum.Variant;
                var v = new Variant
                {
                    _object = value,
                    _header = new DataTypeHeader
                    {
                        TypeEnum = TypeEnum.Matrix,
                        ElementTypeEnum = subTypeEnum
                    }
                };
                return v;
            }

            if (objTypeEnum == TypeEnum.Object)
            {
                var subTypeEnum = KnownTypeAttribute.GetTypeId(value.GetType());
                var v = new Variant
                {
                    _object = value,
                    _header = new DataTypeHeader
                    {
                        TypeEnum = TypeEnum.Object,
                        ElementTypeEnum = (TypeEnum)subTypeEnum
                    }
                };
                return v;
            }

            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object ToObject()
        {
            if (TypeEnum == TypeEnum.None)
            {
                return null;
            }
            // unwrap potentially boxed known types
            var typeEnum = TypeEnum;
            if ((int)typeEnum < KnownSmallTypesLimit)
            {
                switch (typeEnum)
                {
                    case TypeEnum.None:
                        throw new InvalidOperationException("TypeEnum.None is possible only for nulls");
                    case TypeEnum.Int8:
                        return Get<sbyte>();

                    case TypeEnum.Int16:
                        return Get<short>();

                    case TypeEnum.Int32:
                        return Get<int>();

                    case TypeEnum.Int64:
                        return Get<long>();

                    case TypeEnum.UInt8:
                        return Get<byte>();

                    case TypeEnum.UInt16:
                        return Get<ushort>();

                    case TypeEnum.UInt32:
                        return Get<uint>();

                    case TypeEnum.UInt64:
                        return Get<ulong>();

                    case TypeEnum.Float32:
                        return Get<float>();

                    case TypeEnum.Float64:
                        return Get<double>();

                    case TypeEnum.Decimal:
                        return Get<decimal>();

                    case TypeEnum.Price:
                        return Get<Price>();

                    case TypeEnum.Money:
                        throw new NotImplementedException();
                    //return value.Get<Money>();

                    case TypeEnum.DateTime:
                        return Get<DateTime>();

                    case TypeEnum.Timestamp:
                        return Get<Timestamp>();

                    case TypeEnum.Bool:
                        return Get<bool>();

                    case TypeEnum.ErrorCode:
                        return Get<ErrorCode>();

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // all other types are stored directly as objects
            if (_object != null) return _object;

            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T[] array)
        {
            return Create(array, 0, array.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create<T>(T[] array, int offset, int length)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            var v = new Variant
            {
                _object = array,
                _count = length,
                _offset = (ulong)offset,
                _header = new DataTypeHeader
                {
                    TypeEnum = TypeEnum.Array,
                    ElementTypeEnum = VariantHelper<T>.TypeEnum,
#pragma warning disable 618
                    TypeSize = TypeHelper<T>.FixedSize >= 0 ? checked((byte)TypeHelper<T>.FixedSize) : (byte)0 // TODO review
#pragma warning restore 618
                }
            };
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Variant Create(string str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            var v = new Variant
            {
                _object = str,
                _header = new DataTypeHeader { TypeEnum = TypeEnum.String }
            };
            return v;
        }

        public TypeEnum TypeEnum
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null)
                {
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
                if (boxed != null)
                {
                    return KnownTypeSizes[(int)boxed.TypeEnum];
                }
                if (_header.TypeEnum == TypeEnum.FixedBinary)
                {
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
                if (boxed != null)
                {
                    return -1; // inlined scalar has no elements
                }
                if (_header.TypeEnum == TypeEnum.Array)
                {
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
                if (boxed != null)
                {
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
                if (boxed != null)
                {
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
                if (_object == null)
                {
                    return VariantLayout.Pointer;
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null)
                {
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
        public T Get<T>()
        {
            if (VariantHelper<T>.IsInline)
            {
                var te = _boxedTypeEnum.TypeEnum;
                if (te != VariantHelper<T>.TypeEnum)
                {
                    ThrowHelper.ThrowInvalidCastException(); //"Variant type doesn't match typeof(T)"
                    return default(T);
                }

                return Unsafe.Read<T>(Unsafe.AsPointer(ref _data));
            }
            return (T)_object;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(T value)
        {
            if (VariantHelper<T>.IsInline)
            {
                var te = _boxedTypeEnum.TypeEnum;
                if (te != VariantHelper<T>.TypeEnum)
                {
                    throw new InvalidCastException("Variant type doesn't match typeof(T)");
                }
                Unsafe.Write(Unsafe.AsPointer(ref _data), value);
                return;
            }
            // TODO
            ThrowHelper.ThrowNotImplementedException();
        }

        /// <summary>
        /// Read a value T directly from the internal fixed 16-bytes buffer
        /// without any checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T UnsafeGetInilned<T>()
        {
            Debug.Assert(Layout == VariantLayout.Inline);
            return Unsafe.Read<T>(Unsafe.AsPointer(ref _data));
        }

        /// <summary>
        /// Write a value T directly to the internal fixed 16-bytes buffer
        /// without any checks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeSetInlined<T>(T value)
        {
            Debug.Assert(Layout == VariantLayout.Inline);
            Unsafe.Write(Unsafe.AsPointer(ref _data), value);
        }

        /// <summary>
        /// Get value at index
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get<T>(int index)
        {
            // TODO manual impl without span
            //var span = this.Span<T>();
            //return span[index];

            // Object
            if (_object is T[] arrayOfT)
            {
                return arrayOfT[index + (int)_offset];
            }
            return GetSlow<T>(index);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T GetSlow<T>(int index)
        {
            var boxed = _object as BoxedTypeEnum;
            if (boxed != null)
            {
                // Inline;
                if (index == 0)
                {
                    return UnsafeGetInilned<T>();
                }
                throw new IndexOutOfRangeException();
            }
            // Pointer;
            if (_object == null)
            {
                throw new NotImplementedException("Pointer is not supported");
            }
            // Object
            if (index == 0)
            {
                return (T)_object;
            }
            throw new IndexOutOfRangeException();
        }

        /// <summary>
        /// Set value at index
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, T value)
        {
            //var span = Span<T>();
            //span[index] = value;

            // Object
            if (_object is T[] arrayOfT)
            {
                arrayOfT[index + (int)_offset] = value;
            }
            else
            {
                SetSlow(index, value);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void SetSlow<T>(int index, T value)
        {
            var boxed = _object as BoxedTypeEnum;
            if (boxed != null)
            {
                // Inline;
                if (index == 0)
                {
                    UnsafeSetInlined(value);
                    return;
                }
                throw new IndexOutOfRangeException();
            }
            // Pointer;
            if (_object == null)
            {
                throw new NotImplementedException("Pointer is not supported");
            }
            throw new InvalidCastException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> Span<T>()
        {
            if (_object == null)
            {
                // Pointer;
                throw new NotImplementedException();
            }
            var boxed = _object as BoxedTypeEnum;
            if (boxed != null)
            {
                // Inline;
                var teOfT = VariantHelper<T>.TypeEnum;
                var te = _boxedTypeEnum.TypeEnum;
                if (te != teOfT)
                {
                    throw new InvalidCastException("Variant type doesn't match typeof(T)");
                }

                return new Span<T>(Unsafe.AsPointer(ref _data), 1);
            }
            // Object
            var typeEnum = _header.TypeEnum;
            if (typeEnum == TypeEnum.Array)
            {
                var elementEnum = _header.ElementTypeEnum;
                var castElementEnum = VariantHelper<T>.TypeEnum;
                if (elementEnum != castElementEnum)
                {
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
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (_object == null)
                {
                    // Pointer;
                    throw new NotImplementedException();
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null)
                {
                    // count was 1 and if we here, index = 0
                    return this;
                }
                // Object
                var typeEnum = _header.TypeEnum;
                if (typeEnum == TypeEnum.Array)
                {
                    //var elementEnum = _header.ElementTypeEnum;
                    Array array = (Array)_object;
                    var value = array.GetValue(index);
                    var v = Create(value);
                    // Debug.Assert(elementEnum == v.ElementTypeEnum); // TODO review, Variant of Variant is a special case
                    return v;
                }
                throw new InvalidCastException();
            }
            set
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if (_object == null)
                {
                    // Pointer;
                    throw new NotImplementedException();
                }
                var boxed = _object as BoxedTypeEnum;
                if (boxed != null)
                {
                    if (boxed.TypeEnum != value.TypeEnum)
                    {
                        ThrowHelper.ThrowInvalidCastException();
                        return;
                    }
                    // count was 1 and if we are here, index = 0
                    Unsafe.Write(Unsafe.AsPointer(ref _data), value._data);
                    return;
                }
                // Object
                var typeEnum = _header.TypeEnum;
                if (typeEnum == TypeEnum.Array)
                {
                    if (_header.ElementTypeEnum != value.TypeEnum)
                    {
                        throw new InvalidCastException($"{_header.ElementTypeEnum} - {value._header.TypeEnum}");
                    }
                    Array array = (Array)_object;
                    var val = value.ToObject();
                    array.SetValue(val, index);
                    return;
                }
                throw new InvalidCastException();
            }
        }

        internal class BoxedTypeEnum
        {
            private static readonly BoxedTypeEnum[] Cache = new BoxedTypeEnum[KnownSmallTypesLimit];

            internal BoxedTypeEnum(TypeEnum typeEnum)
            {
                TypeEnum = typeEnum;
            }

            public readonly TypeEnum TypeEnum;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static BoxedTypeEnum Get(TypeEnum typeEnum)
            {
                var existing = Cache[(int)typeEnum];
                if (existing != null) return existing;
                var newBoxed = new BoxedTypeEnum(typeEnum);
                Cache[(int)typeEnum] = newBoxed;
                return newBoxed;
            }
        }

        internal class BoxedTypeEnum<T>
        {
            public static BoxedTypeEnum CachedBoxedTypeEnum = new BoxedTypeEnum(VariantHelper<T>.TypeEnum);
        }

        public bool Equals(Variant other)
        {
            if (TypeEnum != other.TypeEnum) return false;
            // None equals None without value checks
            if (TypeEnum == TypeEnum.None) return true;
            if ((int)TypeEnum < KnownSmallTypesLimit)
            {
                return _data.Equals(other._data);
            }
            var thisAsObject = ToObject();
            var otherAsObject = other.ToObject();
            return Equals(thisAsObject, otherAsObject);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Variant && Equals((Variant)obj);
            // NB No, Variant(123) does not equals 123. When this is needed, we could always call ToObject() and compare objects
            //return Equals(this.ToObject(), obj);
        }

        public override int GetHashCode()
        {
            // TODO for known types hash codes must match to the original types, e.g. for int32, etc.
            if (TypeEnum == TypeEnum.None) return 0;
            if ((int)TypeEnum < KnownSmallTypesLimit)
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                var dataPtr = (byte*)Unsafe.AsPointer(ref _data);

                unchecked
                {
                    int hashCode = (int)TypeEnum;
                    hashCode = (hashCode * 397) ^ *(int*)(dataPtr);
                    hashCode = (hashCode * 397) ^ *(int*)(dataPtr + 4);
                    hashCode = (hashCode * 397) ^ *(int*)(dataPtr + 8);
                    hashCode = (hashCode * 397) ^ *(int*)(dataPtr + 12);
                    return hashCode;
                }
            }
            return ToObject().GetHashCode();
        }

        public override string ToString()
        {
            var obj = ToObject();
            return Convert.ToString(obj);
        }

        /// <summary>
        /// Equals operator.
        /// </summary>
        public static bool operator ==(Variant first, Variant second)
        {
            return first.Equals(second);
        }

        /// <summary>
        /// Not equals operator.
        /// </summary>
        public static bool operator !=(Variant first, Variant second)
        {
            return !(first == second);
        }
    }
}