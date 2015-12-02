using System.Collections;
using System.Collections.Generic;

namespace Spreads
{
    public struct SingleSequence<T> : IEnumerable<T> {
        public struct SingleEnumerator : IEnumerator<T> {
            private readonly SingleSequence<T> _parent;
            private bool _couldMove;
            public SingleEnumerator(ref SingleSequence<T> parent) {
                _parent = parent;
                _couldMove = true;
            }
            public T Current => _parent._value;
            object IEnumerator.Current => Current;
            public void Dispose() { }

            public bool MoveNext() {
                if (!_couldMove) return false;
                _couldMove = false;
                return true;
            }
            public void Reset() {
                _couldMove = true;
            }
        }
        private readonly T _value;
        public SingleSequence(T value) {
            _value = value;
        }
        public IEnumerator<T> GetEnumerator() {
            return new SingleEnumerator(ref this);
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return new SingleEnumerator(ref this);
        }
    }

    public struct EmptySequence<T> : IEnumerable<T> {
        public struct EmptyEnumerator : IEnumerator<T> {
            public T Current => default(T);
            object IEnumerator.Current => Current;
            public void Dispose() { }
            public bool MoveNext() {
                return false;
            }
            public void Reset() {
            }
        }
        public IEnumerator<T> GetEnumerator() {
            return new EmptyEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            return new EmptyEnumerator();
        }
    }

}