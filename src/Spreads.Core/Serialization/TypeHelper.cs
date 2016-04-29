using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.Serialization {


    internal class TypeHelper<T> {
        [ThreadStatic]
        private static GCHandle _pinnedArray;
        [ThreadStatic]
        private static bool usePinnedArray;
        [ThreadStatic]
        private static T[] Array;

        private static IntPtr _tgt;
        private static IntPtr _ptr;
        private static bool _isInterface;
        private static IBlittableConverter<T> _instance;

        static TypeHelper() {
            try {
                Size = SizeOf();
                SizeMinus8 = Size - 8;
                SizeMinus4 = Size - 4;
            } catch {
                Size = -1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T PtrToStructure(IntPtr ptr) {
            if (_isInterface) {
                return _instance.FromPtr(ptr);
            }
#if TYPED_REF
            var obj = default(T);
            var tr = __makeref(obj);
            *(IntPtr*)(&tr) = ptr;
            return __refvalue(tr, T);
#else
            try {
                if (!usePinnedArray) return (T)Marshal.PtrToStructure(ptr, typeof(T));
            } catch {
                // throw only once, exceptions are expensive
                usePinnedArray = true;
                if (Array == null) {
                    Array = new T[2];
                    _pinnedArray = GCHandle.Alloc(Array, GCHandleType.Pinned);
                    _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0);
                    _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1);
                }
            }

            var pos = 0;
            while (pos <= SizeMinus8) {
                *(long*)(_tgt + pos) = *(long*)(ptr + pos);
                pos += 8;
            }
            while (pos <= SizeMinus4) {
                *(int*)(_tgt + pos) = *(int*)(ptr + pos);
                pos += 4;
            }
            while (pos < Size) {
                *(byte*)(_tgt + pos) = *(byte*)(ptr + pos);
                pos++;
            }
            var ret = Array[0];
            Array[0] = default(T);
            return ret;

#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void StructureToPtr(T value, IntPtr pointer) {
            if (_isInterface) {
                _instance.ToPtr(value, pointer);
                return;
            }
#if TYPED_REF
            // this is as fast as non-generic methods
            var obj = default(T);
            var tr = __makeref(obj);

            *(IntPtr*)(&tr) = pointer;
            __refvalue(tr, T) = value;
#else
            try {
                if (!usePinnedArray) {
                    Marshal.StructureToPtr(value, pointer, false);
                    return;
                }
            } catch {
                // throw only once, exceptions are expensive
                usePinnedArray = true;
                if (Array == null) {
                    Array = new T[2];
                    _pinnedArray = GCHandle.Alloc(Array, GCHandleType.Pinned);
                    _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0);
                    _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1);
                }
            }

            Array[1] = value;
            var tgt = pointer;

            var pos = 0;
            while (pos <= SizeMinus8) {
                *(long*)(tgt + pos) = *(long*)(_ptr + pos);
                pos += 8;
            }
            while (pos <= SizeMinus4) {
                *(int*)(tgt + pos) = *(int*)(_ptr + pos);
                pos += 4;
            }
            while (pos < Size) {
                *(byte*)(tgt + pos) = *(byte*)(_ptr + pos);
                pos++;
            }
            Array[1] = default(T);
#endif
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
#endif
        }

        private static int SizeOf() {

            if (typeof(T) == typeof(DateTime)) return 8;
            //#if TYPED_REF
            //            unsafe
            //            {
            //                GCHandle handle = default(GCHandle);
            //                try {
            //                    var array = new T[2];
            //                    //handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            //                    TypedReference
            //                        elem1 = __makeref(array[0]),
            //                        elem2 = __makeref(array[1]);
            //                    unsafe
            //                    {
            //                        return (int)((byte*)*(IntPtr*)(&elem2) - (byte*)*(IntPtr*)(&elem1));
            //                    }
            //                } finally {
            //                    //handle.Free();
            //                }
            //            }
            //#else
            //#endif
            try {
                return Marshal.SizeOf(typeof(T));
            } catch {
                try {
                    // throw only once, exceptions are expensive
                    //usePinnedArray = true;
                    if (Array == null) {
                        Array = new T[2];
                    }
                    _pinnedArray = GCHandle.Alloc(Array, GCHandleType.Pinned);
                    _tgt = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0);
                    _ptr = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1);
                    var size = (int)
                        (Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1).ToInt64() -
                         Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0).ToInt64());
                    _pinnedArray.Free();
                    Array = null;
                    return size;
                } catch {

                    // NB we try to check interface as a last step, because some generic types 
                    // could implement IBlittableConverter<T> but still be blittable for certain types,
                    // e.g. DateTime vs long in PersistentMap<K,V>.Entry
                    var tmp = default(T);
                    if (tmp is IBlittableConverter<T>) {
                        _isInterface = true;
                        try {
                            _instance = (IBlittableConverter<T>)Activator.CreateInstance<T>();
                        } catch {
                            //Trace.TraceWarning($"Type {typeof(T).FullName} is marked as IBlittableConverter and so it must have a parameterless constructor");
                            throw new ApplicationException($"Type T ({typeof(T).FullName}) is marked as IBlittable<T> and so it must have a parameterless constructor.");
                        }
                        return _instance.IsBlittable ? ((IBlittableConverter<T>)_instance).Size : -1;
                    }

                    return -1;
                }
            }
        }

        //        internal static int SizeUnsafe() {
        //#if TYPED_REF
        //            unsafe
        //            {
        //                GCHandle handle = default(GCHandle);
        //                var array = new T[2];
        //                var local = __makeref(array);
        //                TypedReference
        //                    elem1 = __makeref(array[0]),
        //                    elem2 = __makeref(array[1]);
        //                unsafe
        //                {
        //                    return (int)((byte*)*(IntPtr*)(&elem2) - (byte*)*(IntPtr*)(&elem1));
        //                }
        //            }
        //#else
        //            throw new NotSupportedException();
        //#endif
        //        }

        /// <summary>
        /// Returns a positive size of a blittable type T, or -1 if the type T is not blittable.
        /// We assume the type T is blittable if `GCHandle.Alloc(T[2], GCHandleType.Pinned) = true`.
        /// This is more relaxed than Marshal.SizeOf, but still doesn't cover cases such as 
        /// and array of KVP[DateTime,double], which has a contiguous layout in memory.
        /// </summary>
        public static int Size { get; }
        private static int SizeMinus8 { get; }
        private static int SizeMinus4 { get; }
    }
}