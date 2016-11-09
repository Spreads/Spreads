// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spreads.DataTypes {

    [Obsolete("This defeats the purspose of Variant targeted as untyped")]
    [StructLayout(LayoutKind.Sequential)]
    public struct Variant<T> {

        public Variant(T value) {
            _value = Variant.Create(value);
        }

        private Variant _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Variant(Variant<T> value) {
            return value._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(Variant<T> value) {
            return value._value.Get<T>();
        }

        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (T)this; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { _value.Set(value); }
        }

        public Variant AsUntyped
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _value; }
        }
    }
}
