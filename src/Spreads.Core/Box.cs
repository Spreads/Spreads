// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace Spreads
{
    public class Box
    {
        protected Box()
        {
        }

        public static Box<T> New<T>(in T unboxed) => new(unboxed);
    }

    public class Box<T> : Box
    {
        public T Value = default!;

        public Box()
        {
        }

        public Box(T value) => Value = value;

        public static explicit operator T(Box<T> boxed) => boxed.Value;

        public static explicit operator Box<T>(in T unboxed) => Box.New(in unboxed);
    }
}
