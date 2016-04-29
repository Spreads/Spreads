using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Serialization {

    /// <summary>
    /// If a type T is IBlittableConverter, it has ability to convert its instances to and from pointers.
    /// An instance of T is required to convert other instances of T. Therefore, we have implicit constraint of `T where : new()`.
    /// </summary>
    public interface IBlittableConverter<T> {
        bool IsBlittable { get; }
        int Size { get; }
        void ToPtr(T value, IntPtr ptr);
        T FromPtr(IntPtr ptr);
    }
}
