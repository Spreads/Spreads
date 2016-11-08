// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;

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
            var type = VariantHelper<T>.GetTypeEnum();
            if (type != value.TypeEnum) throw new ArgumentException("Type mismatch in typed Variant<T>");
            _value = value;
        }

        public static implicit operator Variant(Variant<T> value) {
            return value._value;
        }
    }
}
