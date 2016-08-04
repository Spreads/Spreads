using System;
using System.Collections;
using System.Collections.Generic;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Storage {

    internal class PersistentArray<T> : IEnumerable<T>, IDisposable where T : struct {
        private const int HeaderLength = 256;
        internal static readonly int DataOffset = HeaderLength + TypeHelper<T>.Size;
        public static readonly int ItemSize;
        private readonly DirectFile _df;

        // MMaped pointers in the header for custom use
        internal IntPtr Slot0 => _df.Buffer.Data;
        internal IntPtr Slot1 => _df.Buffer.Data + 8;
        internal IntPtr Slot2 => _df.Buffer.Data + 16;
        internal IntPtr Slot3 => _df.Buffer.Data + 24;
        internal IntPtr Slot4 => _df.Buffer.Data + 32;
        internal IntPtr Slot5 => _df.Buffer.Data + 40;
        internal IntPtr Slot6 => _df.Buffer.Data + 48;
        internal IntPtr Slot7 => _df.Buffer.Data + 56;

        static PersistentArray() {
            ItemSize = TypeHelper<T>.Size;
            if (ItemSize <= 0) throw new InvalidOperationException("PersistentArray<T> supports only fixed-size types");
        }

        private PersistentArray(string filename, long minCapacity, T fill) {
            _df = new DirectFile(filename, DataOffset + minCapacity * ItemSize);
        }

        public PersistentArray(string filename, long minCapacity = 5L) : this(filename, minCapacity, default(T)) {

        }

        internal void Grow(long minCapacity) {
            _df.Grow(DataOffset + minCapacity * ItemSize);
        }

        public void Dispose() {
            _df.Dispose();
        }

        private IEnumerable<T> AsEnumerable() {
            for (int i = 0; i < LongCount; i++) {
                yield return this[i];
            }
        }
        public IEnumerator<T> GetEnumerator() {
            return AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }


        public void Clear() {
            for (int i = 0; i < LongCount; i++) {
                this[i] = default(T);
            }
        }

        public int Count => LongCount > int.MaxValue ? -1 : (int)LongCount;
        public long LongCount => (_df.Capacity - DataOffset) / ItemSize;
        public bool IsReadOnly => false;

        internal DirectBuffer Buffer => _df.Buffer;

        public T this[long index]
        {
            get
            {
                if (index < -1 || index >= LongCount) throw new ArgumentOutOfRangeException();
                T temp = default(T);
                TypeHelper<T>.Read(new IntPtr(_df.Buffer.Data.ToInt64() + (DataOffset + index * ItemSize)), ref temp); //.Read<T>(DataOffset + index * ItemSize);
                return temp;
            }
            set
            {
                if (index < -1 || index >= LongCount) throw new ArgumentOutOfRangeException();
                //_df.Buffer.Write(DataOffset + index * ItemSize, value);
                TypeHelper.Write(value, new IntPtr(_df.Buffer.Data.ToInt64() + (DataOffset + index * ItemSize)));
            }
        }

        //private void Copy(long source, long target) {
        //    _df.Buffer.Copy<T>(DataOffset + source * ItemSize, DataOffset + target * ItemSize);
        //}
    }
}
