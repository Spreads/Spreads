using System;
using System.Collections.Generic;
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
        /// For types with non-fixed size this method performs serialization and
        /// keeps serialized bytes cached until `ToPtr` method is called. If `value`
        /// in `ToPtr` call is equal to the value from `SizeOf` call then the cached bytes
        /// are reused.
        /// </summary>
        int SizeOf(T value);

        /// <summary>
        /// For types with non-fixed size this method performs serialization and
        /// keeps serialized bytes cached until `SizeOf` method is called. If `value`
        /// in `SizeOf` call is equal to the value from `ToPtr` call then the cached bytes
        /// are reused.
        /// </summary>
        void ToPtr(T value, IntPtr ptr);

        T FromPtr(IntPtr ptr);

        /// <summary>
        /// A stub for future versioning support. A value -1 indicates that a type's binary format is set 
        /// in stone and will never change - rather, the type itself and its name will change.
        /// </summary>
        int Version { get; }
    }
}
