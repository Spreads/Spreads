using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Spreads.DataTypes {

    public interface IVariant {
        Variant AsVariant { get; }
    }

    public interface IVariant<T> : IVariant {
        Variant<T> AsTypedVariant { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Variant<T> {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private Variant _value;

        internal Variant(Variant value) {
            var type = Variant.GetTypeCode<T>();
            if (type != value.TypeEnum) throw new ArgumentException("Type mismatch is typed Variant<T>");
            _value = value;
        }

        public static implicit operator Variant(Variant<T> value) {
            return value._value;
        }
    }
}
