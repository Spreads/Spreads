using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Buffers {
    public interface IGenericArrayPool {
        /// <summary>
        /// Increment reference count
        /// </summary>
        int Borrow<T>(T[] buffer);
        int ReferenceCount<T>(T[] buffer);
        /// <summary>
        /// Decrement reference count and return to a pool if ref count is zero
        /// </summary>
        int Return<T>(T[] buffer);
        /// <summary>
        /// Take an array from a pool
        /// </summary>
        T[] Take<T>(int minimumLength);
    }
}
