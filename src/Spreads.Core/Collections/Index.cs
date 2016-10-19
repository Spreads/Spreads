using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.Slices;
using System.Buffers;

namespace Spreads.Collections {
    public interface IIndex<T> {
        int OffsetOf(T item);
        T ItemAt(int offset);
        IEnumerable<T> Items { get; }
        IComparer<T> Comparer { get; }
        long Count { get; }
        bool IsSorted { get; }
        T First { get; }
        T Last { get; }
    }


    public class SpanIndex<T> : IIndex<T>, IDisposable {
        internal Span<T> _items;
        internal int _count;
        internal IComparer<T> _comparer;
        internal bool _isSorted;
        /// <summary>
        /// We could reuse index, e.g. when we take a column from a table.
        /// In-memory indicies use array pool and must return items array to the pool.
        /// </summary>
        internal int _refCount;
        internal SpanIndex<T> _parent;

        private SpanIndex() { }

        public SpanIndex(int minCapacity) {
            var array = ArrayPool<T>.Shared.Rent(minCapacity);
            _items = new Span<T>(array, 0);
            _parent = this;
            _comparer = Comparer<T>.Default;
        }

        internal SpanIndex<T> Rent(int offset, int count) {
            if ((uint)offset + (uint)count > _count) throw new ArgumentException("Wrong offset + count");
            var idx = new SpanIndex<T>();
            var items = _items.Slice(offset);
            idx._items = items;
            idx._count = count;
            idx._parent = this;
            Interlocked.Increment(ref idx._parent._refCount);
            return idx;
        }

        public void Dispose() {
            var remaining = Interlocked.Decrement(ref _parent._refCount);
            if (remaining < 0 && _items.Object != null) {
                ArrayPool<T>.Shared.Return(_items.Object as T[]);
            }
        }

        public int OffsetOf(T item) {
            if (_isSorted) {
                return _items.BinarySearch<T>(0, _count, item, _comparer);
            } else {
                for (int i = 0; i < _count; i++) {
                    if (_comparer.Compare(_items[i], item) == 0) {
                        return i;
                    }
                }
                return (-1);
            }
        }

        public T ItemAt(int offset) {
            var idx = checked((int)((long)offset));
            return _items[idx];
        }

        public IEnumerable<T> Items => _items;
        public IComparer<T> Comparer => _comparer;
        public long Count => _count;
        public bool IsSorted => _isSorted;
        public T First => _items[0];
        public T Last => _items[checked((int)_count)];
    }

}
