using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Serialization {

    // TODO decima special case, it is not primitive

    // TODO(!!!) find all occurences of TH and replace with BinarySerializer

    internal delegate int FromPtrDelegate(IntPtr ptr, ref object value);
    internal delegate int ToPtrDelegate(object value, ref DirectBuffer destination, uint offset = 0u, MemoryStream ms = null);
    internal delegate int SizeOfDelegate(object value, out MemoryStream memoryStream);

    internal class TypeParams {
        public int Size;
        /// <summary>
        /// CLR definition, we cache it here since ty.IsValueType is a virtual call
        /// </summary>
        public bool IsValueType;
        /// <summary>
        /// Either CLR-primitive or a pinnale struct marked with SerializationAttribute(BlittableSize > 0)
        /// </summary>
        public bool IsBlittable;
        public bool IsFixedSize;
        public bool IsDateTime;

    }

    internal class TypeHelper {

        private static int Read<T>(IntPtr ptr, ref object value) {
            var temp = value == null ? default(T) : (T)value;
            var len = TypeHelper<T>.Read(ptr, ref temp);
            value = temp;
            return len;
        }

        private static int Write<T>(object value, ref DirectBuffer destination, uint offset = 0u, MemoryStream ms = null) {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.Write(temp, ref destination, offset, ms);
        }

        private static int SizeOf<T>(object value, out MemoryStream memoryStream) {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.SizeOf(temp, out memoryStream);
        }

        private static int Size<T>() {
            return TypeHelper<T>.Size;
        }

        private static readonly Dictionary<Type, FromPtrDelegate> FromPtrDelegateCache = new Dictionary<Type, FromPtrDelegate>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FromPtrDelegate GetFromPtrDelegate(Type ty) {
            FromPtrDelegate temp;
            if (FromPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("Read", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (FromPtrDelegate)Delegate.CreateDelegate(typeof(FromPtrDelegate), genericMi);
            FromPtrDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, ToPtrDelegate> ToPtrDelegateCache = new Dictionary<Type, ToPtrDelegate>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ToPtrDelegate GetToPtrDelegate(Type ty) {
            ToPtrDelegate temp;
            if (ToPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("Write", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (ToPtrDelegate)Delegate.CreateDelegate(typeof(ToPtrDelegate), genericMi);
            ToPtrDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, SizeOfDelegate> SizeOfDelegateCache = new Dictionary<Type, SizeOfDelegate>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SizeOfDelegate GetSizeOfDelegate(Type ty) {
            SizeOfDelegate temp;
            if (SizeOfDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("SizeOf", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (SizeOfDelegate)Delegate.CreateDelegate(typeof(SizeOfDelegate), genericMi);
            SizeOfDelegateCache[ty] = temp;
            return temp;
        }

        private static readonly Dictionary<Type, int> SizeDelegateCache = new Dictionary<Type, int>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(Type ty) {
            int temp;
            if (SizeDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("Size", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var genericMi = mi.MakeGenericMethod(ty);
            temp = (int)genericMi.Invoke(null, new object[] { });
            SizeDelegateCache[ty] = temp;
            return temp;
        }
    }

    internal unsafe sealed class TypeHelper<T> : TypeHelper {

        // ReSharper disable once StaticMemberInGenericType
        private static bool _hasBinaryConverter;
#if !TYPED_REF
        //private static bool _usePinnedArray;
        // ReSharper disable once StaticMemberInGenericType
        private static GCHandle _pinnedArray;
        // ReSharper disable once StaticMemberInGenericType
        private static bool _isDateTime; // NB: Automatic layout of .NET requires special handling!
        private static bool _isDecimal;
        private static T[] _array;
        private static IntPtr _tgt;
        private static IntPtr _ptr;
#endif
        private static IBinaryConverter<T> _convertorInstance;
        private static int _size;
        private static TypeParams _typeParams = new TypeParams();

        static TypeHelper() {
            try {
                _size = Init();
            } catch {
                _size = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PinnedSize() {
            try {
                var array = new T[2];
                var pinnedArrayHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
                var size = (int)
                    (Marshal.UnsafeAddrOfPinnedArrayElement(array, 1).ToInt64() -
                     Marshal.UnsafeAddrOfPinnedArrayElement(array, 0).ToInt64());
                pinnedArrayHandle.Free();
                return size;
            } catch {
                return -1;
            }
        }

        /// <summary>
        /// Method is only called from the static constructor of TypeHelper.
        /// </summary>
        /// <returns></returns>
        private static int Init() {
            var ty = typeof(T);
            if (ty == typeof(DateTime)) {
#if !TYPED_REF
                _isDateTime = true;
#endif
                _typeParams.IsBlittable = true;
                _typeParams.IsFixedSize = true;
                _typeParams.IsDateTime = true;
                _typeParams.Size = 8;
                return 8;
            }
            if (ty == typeof(decimal)) {
#if !TYPED_REF
                _isDecimal = true;
#endif
                _typeParams.IsBlittable = true;
                _typeParams.IsFixedSize = true;
                _typeParams.IsDateTime = true;
                _typeParams.Size = 16;
                return 16;
            }

            _typeParams.IsValueType = ty.IsValueType;
            var pinnedSize = PinnedSize();

            if (pinnedSize > 0) {
                if (ty.IsPrimitive && (ty != typeof(bool) && ty != typeof(char))) {
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }

                // for a non-primitive type to be blittable, it must have an attribute
                var sa = SerializationAttribute.GetSerializationAttribute(ty);
                var sla = ty.StructLayoutAttribute;
                var hasSizeAttribute = false;
                if (sa != null && sa.BlittableSize > 0) {
                    if (pinnedSize != sa.BlittableSize) {
                        Environment.FailFast($"Size of type {ty.Name} defined in SerializationAttribute {sa.BlittableSize} differs from calculated size {pinnedSize}.");
                    }
                    hasSizeAttribute = true;
                }
                if (sla != null && sla.Size > 0) {
                    if (pinnedSize != sla.Size) {
                        Environment.FailFast($"Size of type {ty.Name} defined in StructLayoutAttribute {sla.Size} differs from calculated size {pinnedSize}.");
                    }
                    hasSizeAttribute = true;
                }
                if (hasSizeAttribute) {
                    if (typeof(IBinaryConverter<T>).IsAssignableFrom(ty)) {
                        // NB: this makes no sense, because blittable is version 0, if we have any change 
                        // to struct layout later, we won't be able to work with version 0 anymore
                        // and will lose ability to work with old values.
                        Environment.FailFast($"Blittable types must not implement IBinaryConverter<T> interface.");
                    }
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }
                if (sa != null && sa.PreferBlittable) {
                    // NB: here it is OK to have an interface, we just opt-in for blittable
                    // when we know it won't change, e.g. generic struct with fixed fields (KV<K,V>, DictEntry<K,V>, Message<T>, etc.)
                    // usually those types are internal
                    _typeParams.IsBlittable = true;
                    _typeParams.IsFixedSize = true;
                    _typeParams.Size = pinnedSize;
                    return pinnedSize;
                }
            }

            // by this line the type is not blittable
            _typeParams.IsBlittable = false;

            // NB we try to check interface as a last step, because some generic types 
            // could implement IBinaryConverter<T> but still be blittable for certain types,
            // e.g. DateTime vs long in PersistentMap<K,V>.Entry
            //if (tmp is IBinaryConverter<T>) {
            if (typeof(IBinaryConverter<T>).IsAssignableFrom(ty)) {
                IBinaryConverter<T> convertor;
                try {
                    convertor = (IBinaryConverter<T>)Activator.CreateInstance<T>();
                } catch {
                    //Trace.TraceWarning($"Type {typeof(T).FullName} is marked as IBinaryConverter and so it must have a parameterless constructor");
                    throw new ApplicationException($"Type T ({typeof(T).FullName}) is marked as IBinaryConverter<T> and so it must have a parameterless constructor.");
                }
                if (convertor.Version <= 0) {
                    throw new InvalidOperationException("IBinaryConverter<T> implementation for a type T should have a positive version.");
                }
                _convertorInstance = convertor;
                _hasBinaryConverter = true;
                return _convertorInstance.IsFixedSize ? _convertorInstance.Size : 0;
            }
            //byte[] should work as any other primitive array
            if (ty == typeof(byte[])) {
                _convertorInstance = (IBinaryConverter<T>)(new ByteArrayBinaryConverter());
                _hasBinaryConverter = true;
                return 0;
            }
            if (ty == typeof(string)) {
                _convertorInstance = (IBinaryConverter<T>)(new StringBinaryConverter());
                _hasBinaryConverter = true;
                return 0;
            }
            if (ty.IsArray) {
                var elementType = ty.GetElementType();
                var elementSize = GetSize(elementType);
                if (elementSize > 0) { // only for blittable types
                    var convertor = (IBinaryConverter<T>)BlittableArrayConvertorFactory.Create(elementType);
                    if (convertor == null) return -1;
                    _convertorInstance = convertor;
                    _hasBinaryConverter = true;
                    Trace.Assert(!_convertorInstance.IsFixedSize);
                    Trace.Assert(_convertorInstance.Size == 0);
                    return 0;
                }
            }
            return -1;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Read(IntPtr ptr, ref T value) {
            if (_hasBinaryConverter) {
                Debug.Assert(_size == 0);
                return _convertorInstance.Read(ptr, ref value);
            }
            if (_size < 0) {
                throw new InvalidOperationException("TypeHelper<T> doesn't support variable-size types");
            }
            Debug.Assert(_size > 0);
#if TYPED_REF
            var obj = default(T);
            var tr = __makeref(obj);
            *(IntPtr*)(&tr) = ptr;
            value = __refvalue(tr, T);
            return _size;
#else
            if (_isDateTime) {
                value = (T)(object)*(DateTime*)ptr;
                return 8;
            }
            if (_isDecimal) {
                value = (T)(object)*(decimal*)ptr;
                return 16;
            }

            if (_array == null) {
                _array = new T[2];
                _pinnedArray = GCHandle.Alloc(_array, GCHandleType.Pinned);
                _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
                _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1);
            }

            ByteUtil.MemoryCopy((byte*)_tgt, (byte*)ptr, (uint)_size);
            var ret = _array[0];
            _array[0] = default(T);
            value = ret;
            return _size;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Write(T value, ref DirectBuffer destination, uint offset = 0u, MemoryStream ms = null) {
            if (_hasBinaryConverter) {
                Debug.Assert(_size == 0);
                return _convertorInstance.Write(value, ref destination, offset, ms);
            }
            if (Size < 0) {
                throw new InvalidOperationException("TypeHelper<T> doesn't support variable-size types");
            }
            Debug.Assert(_size > 0);
            if (!destination.HasCapacity(offset, _size)) return (int)BinaryConverterErrorCode.NotEnoughCapacity;
            var pointer = destination.Data + (int) offset;
#if TYPED_REF
            // this is as fast as non-generic methods
            var obj = default(T);
            var tr = __makeref(obj);
            *(IntPtr*)(&tr) = pointer;
            __refvalue(tr, T) = value;
#else
            if (_isDateTime) {
                // TODO http://stackoverflow.com/a/3344181/801189
                // Code gen is probably needed to avoid boxing when TYPED_REF is not available
                *(DateTime*)pointer = (DateTime)(object)value;
                return 8;
            }
            if (_isDecimal) {
                *(decimal*)pointer = (decimal)(object)value;
                return 16;
            }

            if (_array == null) {
                _array = new T[2];
                _pinnedArray = GCHandle.Alloc(_array, GCHandleType.Pinned);
                _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
                _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1);
            }

            _array[1] = value;
            var tgt = pointer;
            ByteUtil.MemoryCopy((byte*)tgt, (byte*)_ptr, (uint)Size);
            _array[1] = default(T);
#endif
            return _size;
        }




        /// <summary>
        /// Returns binary size of the value
        /// </summary>
        /// <param name="value"></param>
        /// <param name="memoryStream"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SizeOf(T value, out MemoryStream memoryStream) {
            if (_hasBinaryConverter) {
                Debug.Assert(_size == 0);
                return _convertorInstance.SizeOf(value, out memoryStream);
            }
            if (_size < 0) {
                throw new InvalidOperationException("TypeHelper<T> doesn't support variable-size types");
            }
            Debug.Assert(_size > 0);
            memoryStream = null;
            return _size;
        }


        /// <summary>
        /// Returns a positive size of a blittable type T, -1 if the type T is not blittable and has 
        /// no registered converter, 0 is there is a registered converter for variable-length type.
        /// We assume the type T is blittable if `GCHandle.Alloc(T[2], GCHandleType.Pinned) = true`.
        /// This is more relaxed than Marshal.SizeOf, but still doesn't cover cases such as 
        /// an array of KVP[DateTime,double], which has a contiguous layout in memory.
        /// </summary>
        public static int Size => _size;

        public static bool IsBlittable => _size > 0;

        public static byte Version => _hasBinaryConverter ? _convertorInstance.Version : (byte)0;


        internal static void RegisterConvertor(IBinaryConverter<T> convertor, bool overrideExisting = false) {
            if (convertor == null) throw new ArgumentNullException(nameof(convertor));
            if (Size > 0) throw new InvalidOperationException("Cannot register a custom convertor for fixed-size types");

            // NB TypeHelper is internal, we could provide some hooks later e.g. for char or bool
            if (convertor.Version == 0) {
                Trace.TraceWarning("Adding a convertor with version zero");
            }

            if (_hasBinaryConverter && !overrideExisting)
                throw new InvalidOperationException(
                    $"Type {typeof(T)} already implements IBinaryConverter<{typeof(T)}> interface. Use versioning to add a new convertor (not supported yet)");

            if (_typeParams.IsBlittable) {
                Environment.FailFast($"Blittable types must not have IBinaryConverter<T>.");
            }
            _hasBinaryConverter = true;
            _convertorInstance = convertor;
            _size = convertor.Size;
        }
    }
}