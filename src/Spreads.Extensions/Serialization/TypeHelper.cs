using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization {

    // TODO(!!!) find all occurences of TH and replace with BinarySerializer

    internal delegate int FromPtrDelegate(IntPtr ptr, ref object value);
    internal delegate int ToPtrDelegate(object value, IntPtr pointer, MemoryStream ms = null);
    internal delegate int SizeOfDelegate(object value, out MemoryStream memoryStream);

    internal class TypeParams {
        public int Size { get; set; }
    }


    internal class TypeHelper {

        internal static int FromPtr<T>(IntPtr ptr, ref object value) {
            var temp = value == null ? default(T) : (T)value;
            var len = TypeHelper<T>.FromPtr(ptr, ref temp);
            value = temp;
            return len;
        }

        internal static int ToPtr<T>(object value, IntPtr pointer, MemoryStream ms = null) {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.ToPtr(temp, pointer, ms);
        }

        internal static int SizeOf<T>(object value, out MemoryStream memoryStream) {
            var temp = value == null ? default(T) : (T)value;
            return TypeHelper<T>.SizeOf(temp, out memoryStream);
        }

        internal static int Size<T>() {
            return TypeHelper<T>.Size;
        }

        private static readonly Dictionary<Type, FromPtrDelegate> FromPtrDelegateCache = new Dictionary<Type, FromPtrDelegate>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static FromPtrDelegate GetFromPtrDelegate(Type ty) {
            FromPtrDelegate temp;
            if (FromPtrDelegateCache.TryGetValue(ty, out temp)) return temp;
            var mi = typeof(TypeHelper).GetMethod("FromPtr", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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
            var mi = typeof(TypeHelper).GetMethod("ToPtr", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
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
        private static GCHandle _pinnedArray;
        // ReSharper disable once StaticMemberInGenericType
        private static bool _usePinnedArray;
        private static T[] _array;
        private static IntPtr _tgt;
        private static IntPtr _ptr;
        private static bool _hasBinaryConverter;
#if !TYPED_REF
        private static bool _isDateTime; // NB: Automatic layout of .NET requires special handling!
#endif
        private static IBinaryConverter<T> _convertorInstance;
        private static int _size;


        static TypeHelper() {
            try {
                _size = Init();
            } catch {
                _size = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromPtr(IntPtr ptr, ref T value) {

            if (_hasBinaryConverter) {
                return _convertorInstance.FromPtr(ptr, ref value);
            }
            if (Size <= 0) {
                throw new InvalidOperationException();
                //var version = Marshal.ReadInt32(ptr);
                //var length = Marshal.ReadInt32(ptr + 4);
                //// TODO by ref
                //value = Serializer.Deserialize<T>(ptr + 8, length);
                //return length + 8;
            }

#if TYPED_REF
            var obj = default(T);
            var tr = __makeref(obj);
            *(IntPtr*)(&tr) = ptr;
            value = __refvalue(tr, T);
            return Size;
#else
            if (_isDateTime) {
                return (T)(object)*(DateTime*)ptr;
            }
            try {
                if (!_usePinnedArray) return (T)Marshal.PtrToStructure(ptr, typeof(T));
            } catch {
                // throw only once, exceptions are expensive
                _usePinnedArray = true;
                if (_array == null) {
                    _array = new T[2];
                    _pinnedArray = GCHandle.Alloc(_array, GCHandleType.Pinned);
                    _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
                    _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1);
                }
            }

            ByteUtil.MemoryCopy(_tgt, ptr, (uint)Size);
            var ret = _array[0];
            _array[0] = default(T);
            return ret;

#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToPtr(T value, IntPtr pointer, MemoryStream ms = null) {
            if (_hasBinaryConverter) {
                return _convertorInstance.ToPtr(value, pointer, ms);

            }

            if (Size < 0) {
                throw new InvalidOperationException();
                //var bytes = Serializer.Serialize(value);
                //TypeHelper<byte[]>.ToPtr(bytes, pointer);
                //return;
            }

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
                *(DateTime*)pointer = Convert.ToDateTime(value);
                return;
            }
            try {
                if (!_usePinnedArray) {
                    Marshal.StructureToPtr(value, pointer, false);
                    return;
                }
            } catch {
                // throw only once, exceptions are expensive
                _usePinnedArray = true;
                if (_array == null) {
                    _array = new T[2];
                    _pinnedArray = GCHandle.Alloc(_array, GCHandleType.Pinned);
                    _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
                    _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1);
                }
            }

            _array[1] = value;
            var tgt = pointer;
            ByteUtil.MemoryCopy(tgt, _ptr, (uint)Size);
            _array[1] = default(T);
#endif
            return _size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void PtrToPtr(IntPtr source, IntPtr target) {

#if TYPED_REF
            var obj = default(T);
            var tr = __makeref(obj);
            *(IntPtr*)(&tr) = source;
            T value = __refvalue(tr, T);
            *(IntPtr*)(&tr) = target;
            __refvalue(tr, T) = value;

#else
            throw new NotImplementedException();
#endif
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
                return 8;
            }

            //try {
            //    return Marshal.SizeOf(ty);
            //} catch {
            try {
                // throw only once, exceptions are expensive
                //usePinnedArray = true;
                if (_array == null) {
                    _array = new T[2];
                }
                _pinnedArray = GCHandle.Alloc(_array, GCHandleType.Pinned);
                _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
                _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1);
                var size = (int)
                    (Marshal.UnsafeAddrOfPinnedArrayElement(_array, 1).ToInt64() -
                     Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0).ToInt64());
                _pinnedArray.Free();
                _array = null;
                return size;
            } catch {
                // NB we try to check interface as a last step, because some generic types 
                // could implement IBinaryConverter<T> but still be blittable for certain types,
                // e.g. DateTime vs long in PersistentMap<K,V>.Entry
                var tmp = default(T);
                if (tmp is IBinaryConverter<T>) {
                    _hasBinaryConverter = true;
                    IBinaryConverter<T> convertor;
                    try {
                        convertor = (IBinaryConverter<T>)Activator.CreateInstance<T>();
                    } catch {
                        //Trace.TraceWarning($"Type {typeof(T).FullName} is marked as IBinaryConverter and so it must have a parameterless constructor");
                        throw new ApplicationException($"Type T ({typeof(T).FullName}) is marked as IBinaryConverter<T> and so it must have a parameterless constructor.");
                    }
                    if (convertor.Version > 0) throw new InvalidOperationException("A type T implementing IBinaryConverter<T> should have default version. Register a custom convertor for versioning.");
                    _convertorInstance = convertor;
                    return _convertorInstance.IsFixedSize ? _convertorInstance.Size : 0;
                }
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
                //if (ty.IsArray) {
                //    Console.WriteLine("IsArray");
                //    var elementType = ty.GetElementType();
                //    var convertor = (IBinaryConverter<T>)ArrayConvertorFactory.Create(elementType);
                //    if (convertor != null) {
                //        _convertorInstance = convertor;
                //        _hasBinaryConverter = true;
                //        Trace.Assert(!_convertorInstance.IsFixedSize);
                //        Trace.Assert(_convertorInstance.Size == 0);
                //        return 0;
                //    }
                //}
                return -1;
            }
            //}
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
                memoryStream = null;
                return -1;
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

        /// <summary>
        /// If type is supported by TypeHelper, is it either a blittable fixed-size type (Size > 0) or 
        /// a type with a binary convertor (Size = 0).
        /// </summary>
        //public static bool IsSupportedType => _size >= 0;

        public static int Version => _hasBinaryConverter ? _convertorInstance.Version : 0;

        public static void RegisterConvertor(IBinaryConverter<T> convertor, bool overrideExisting = false) {
            if (convertor == null) throw new ArgumentNullException(nameof(convertor));
            if (Size > 0) throw new InvalidOperationException("Cannot register a custom convertor for fixed-size types");
            if (_hasBinaryConverter && !overrideExisting)
                throw new InvalidOperationException(
                    $"Type {typeof(T)} already implements IBinaryConverter<{typeof(T)}> interface. Use versioning to add a new convertor (not supported yet)");
            if (convertor.Version > 0) throw new NotImplementedException("Serialization versioning is not supported");
            _hasBinaryConverter = true;
            _convertorInstance = convertor;
            _size = convertor.Size;
        }
    }
}