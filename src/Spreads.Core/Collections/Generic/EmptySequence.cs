// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections;
using System.Collections.Generic;

namespace Spreads.Collections.Generic
{
    public struct EmptySequence<T> : IEnumerable<T>
    {
        public struct EmptyEnumerator : IEnumerator<T>
        {
            public T Current => default(T);
            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                return false;
            }

            public void Reset()
            {
            }
        }

        public EmptyEnumerator GetEnumerator()
        {
            return new EmptyEnumerator();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new EmptyEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new EmptyEnumerator();
        }
    }
}