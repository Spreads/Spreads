using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Buffers {
    public class GenericArrayPool : IGenericArrayPool {

        private class AtomicCounter {
            public int Count;
        }
        private ConditionalWeakTable<object, AtomicCounter> _cwt = new ConditionalWeakTable<object, AtomicCounter>();

        public int Borrow<T>(T[] buffer) {
            AtomicCounter cnt;
            if (!_cwt.TryGetValue(buffer, out cnt)) return 0;
            var count = Interlocked.Increment(ref cnt.Count);
            return count;
        }

        public int ReferenceCount<T>(T[] value) {
            AtomicCounter cnt;
            return _cwt.TryGetValue(value, out cnt) ? Interlocked.Add(ref cnt.Count, 0) : 0;
        }

        public int Return<T>(T[] buffer) {
            AtomicCounter cnt;
            int ret = 0;
            if (_cwt.TryGetValue(buffer, out cnt)) {
                ret = Interlocked.Decrement(ref cnt.Count);
                if (ret == 0) {
                    ArrayPool<T>.Shared.Return(buffer, true);
                }
            }
            return ret;
        }

        public T[] Take<T>(int minimumLength) {
            var buffer = ArrayPool<T>.Shared.Rent(minimumLength);
            AtomicCounter cnt;
            if (_cwt.TryGetValue(buffer, out cnt)) {
                Interlocked.Increment(ref cnt.Count);
            } else {
                _cwt.Add(buffer, new AtomicCounter { Count = 1 });
            }
            return buffer;
        }
    }
}