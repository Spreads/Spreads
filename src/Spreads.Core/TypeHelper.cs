using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads {


    internal class TypeHelper<T> where T : struct {
        [ThreadStatic]
        private static GCHandle _pinnedArray;
        [ThreadStatic]
        private static bool usePinnedArray;
        [ThreadStatic]
        private static T[] Array;

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
                }
            }
            var tgt = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0);
            var pos = 0;
            while (pos <= SizeMinus8) {
                *(long*)(tgt + pos) = *(long*)(ptr + pos);
                pos += 8;
            }
            while (pos <= SizeMinus4) {
                *(int*)(tgt + pos) = *(int*)(ptr + pos);
                pos += 4;
            }
            while (pos < Size) {
                *(byte*)(tgt + pos) = *(byte*)(ptr + pos);
                pos++;
            }
            var ret = Array[0];
            Array[0] = default(T);
            return ret;

#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void StructureToPtr(T value, IntPtr pointer) {

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
                }
            }

            Array[1] = value;
            var tgt = pointer;
            var ptr = Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1);
            var pos = 0;
            while (pos <= SizeMinus8) {
                *(long*)(tgt + pos) = *(long*)(ptr + pos);
                pos += 8;
            }
            while (pos <= SizeMinus4) {
                *(int*)(tgt + pos) = *(int*)(ptr + pos);
                pos += 4;
            }
            while (pos < Size) {
                *(byte*)(tgt + pos) = *(byte*)(ptr + pos);
                pos++;
            }
            Array[1] = default(T);
#endif
        }

        private static int SizeOf() {
#if TYPED_REF
            unsafe
            {
                GCHandle handle = default(GCHandle);
                try {
                    var array = new T[2];
                    handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                    TypedReference
                        elem1 = __makeref(array[0]),
                        elem2 = __makeref(array[1]);
                    unsafe
                    {
                        return (int)((byte*)*(IntPtr*)(&elem2) - (byte*)*(IntPtr*)(&elem1));
                    }
                } finally {
                    handle.Free();
                }
            }
#else
            try {
                if (!usePinnedArray) return Marshal.SizeOf(typeof(T));
            } catch {
                // throw only once, exceptions are expensive
                usePinnedArray = true;
                if (Array == null) {
                    Array = new T[2];
                    _pinnedArray = GCHandle.Alloc(Array, GCHandleType.Pinned);
                }
            }
            return
                (int)
                    (Marshal.UnsafeAddrOfPinnedArrayElement(Array, 1).ToInt64() -
                     Marshal.UnsafeAddrOfPinnedArrayElement(Array, 0).ToInt64());

#endif
        }

        public static int Size { get; }
        private static int SizeMinus8 { get; }
        private static int SizeMinus4 { get; }
    }
}