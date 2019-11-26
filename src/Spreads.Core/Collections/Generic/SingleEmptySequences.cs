// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections;
using System.Collections.Generic;

namespace Spreads.Collections.Generic
{
    public struct SingleSequence<T> : IEnumerable<T>
    {
        public struct SingleEnumerator : IEnumerator<T>
        {
            private readonly SingleSequence<T> _parent;
            private bool _couldMove;

            public SingleEnumerator(ref SingleSequence<T> parent)
            {
                _parent = parent;
                _couldMove = true;
            }

            public T Current => _parent._value;
            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (!_couldMove) return false;
                _couldMove = false;
                return true;
            }

            public void Reset()
            {
                _couldMove = true;
            }
        }

        private readonly T _value;

        public SingleSequence(T value)
        {
            _value = value;
        }

        public SingleEnumerator GetEnumerator()
        {
            return new SingleEnumerator(ref this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new SingleEnumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SingleEnumerator(ref this);
        }
    }
}
