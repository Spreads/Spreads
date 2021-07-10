// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads
{
    /// <summary>
    /// Base class and convenient <see cref="New{T}"/> method for <see cref="Box{T}"/>.
    /// </summary>
    public class Box
    {
        protected Box()
        {
        }

        public static Box<T> New<T>(in T unboxed) => new(unboxed);
    }

    /// <summary>
    /// Explicit strongly-typed box for <typeparamref name="T"/> (as in .NET's boxing concept)
    /// </summary>
    public class Box<T> : Box
    {
        public T Value = default!;

        public Box()
        {
        }

        public Box(T value) => Value = value;

        public static explicit operator T(Box<T> boxed) => boxed.Value;

        public static explicit operator Box<T>(in T unboxed) => New(in unboxed);
    }
}
