using System;
using System.Runtime.InteropServices;

namespace Spreads.Serialization
{
    /// <summary>
    /// Helper class for generic array pointers
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete]
    internal struct GenericArrayPinner<T> : IDisposable {
        GCHandle _pinnedArray;
        private T[] _arr;
        public GenericArrayPinner(T[] arr) {
            _pinnedArray = GCHandle.Alloc(arr, GCHandleType.Pinned);
            _arr = arr;
        }
        public static implicit operator IntPtr(GenericArrayPinner<T> ap) {

            return ap._pinnedArray.AddrOfPinnedObject();
        }

        /// <summary>
        /// Get unmanaged poinetr to the nth element of generic array
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public IntPtr GetNthPointer(int n) {
            return Marshal.UnsafeAddrOfPinnedArrayElement(this._arr, n);
        }

        public void Dispose() {
            _pinnedArray.Free();
            _arr = null;
        }
    }
}