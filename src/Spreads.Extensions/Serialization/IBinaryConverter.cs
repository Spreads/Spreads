using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Serialization {

    /// <summary>
    /// If a type T is IBinaryConverter, it has ability to convert its instances to and from pointers.
    /// An instance of T is required to convert other instances of T. 
    /// Therefore, we have implicit constraint of `T where : new()` or need to register a converter separately (TODO).
    /// </summary>
    public interface IBinaryConverter<T> {
        bool IsFixedSize { get; }

        int Size { get; }

        /// <summary>
        /// For types with non-fixed size this method serializes value into the memoryStream
        /// </summary>
        int SizeOf(T value, out MemoryStream memoryStream);

        /// <summary>
        /// For types with non-fixed size this method assumes that the pointer has enough capacity.
        /// Use SizeOf method to determine the bytes size of the value before writing. If SizeOf
        /// sets memoryStream then write its content directly, otherwise ToPtr will do the same serialization job twice.
        /// </summary>
        void ToPtr(T value, IntPtr ptr);

        T FromPtr(IntPtr ptr);

        /// <summary>
        /// A stub for future versioning support. Values less or equal to zero  
        /// </summary>
        int Version { get; }
    }
}
