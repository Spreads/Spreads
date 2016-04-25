using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.Collections {

    public class DirectArray<T> : IEnumerable<T>, IDisposable where T : struct {
        internal const int HeaderLength = 256;
        internal static int DataOffset = HeaderLength + TypeHelper<T>.Size;
        private readonly string _filename;
        private long _capacity;
        public static readonly int ItemSize;
        private MemoryMappedFile _mmf;
        private readonly FileStream _fileStream;
        private DirectBuffer _buffer;

        // MMaped pointers in the header for custom use
        internal IntPtr Slot0 => _buffer._data;
        internal IntPtr Slot1 => _buffer._data + 8;
        internal IntPtr Slot2 => _buffer._data + 16;
        internal IntPtr Slot3 => _buffer._data + 24;
        internal IntPtr Slot4 => _buffer._data + 32;
        internal IntPtr Slot5 => _buffer._data + 40;
        internal IntPtr Slot6 => _buffer._data + 48;
        internal IntPtr Slot7 => _buffer._data + 56;

        static DirectArray() {
            // start with only blittables as POC
            ItemSize = TypeHelper<T>.Size; // Marshal.SizeOf(typeof(T));
        }

        public DirectArray(string filename, long capacity = 4L) {
            _filename = filename;
            _fileStream = new FileStream(_filename, FileMode.OpenOrCreate,
                            FileAccess.ReadWrite, FileShare.ReadWrite, 4096,
                            //FileOptions.Asynchronous | FileOptions.RandomAccess);
                            FileOptions.None);
            Grow(capacity);
        }

        public void Grow(long newCapacity) {
            if (newCapacity <= _capacity) return;
            _capacity = newCapacity;
            var mmfs = new MemoryMappedFileSecurity();

            var bytesCapacity = Math.Max(_fileStream.Length, DataOffset + newCapacity * ItemSize);
            _mmf?.Dispose();
            var mmf = MemoryMappedFile.CreateFromFile(_fileStream,
                Path.GetFileName(_filename), bytesCapacity,
                MemoryMappedFileAccess.ReadWrite, mmfs, HandleInheritability.Inheritable,
                true);
            // TODO sync
            _mmf = mmf;

            unsafe
            {
                byte* ptr = (byte*)0;
                _mmf.CreateViewAccessor().SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                var ptrV = new IntPtr(ptr);
                _buffer = new DirectBuffer(bytesCapacity, ptrV);
            }

        }

        public void Dispose() {
            _mmf.Dispose();
            _fileStream.Close();
        }

        private IEnumerable<T> AsEnumerable() {
            for (int i = 0; i < _capacity; i++) {
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
            for (int i = 0; i < _capacity; i++) {
                this[i] = default(T);
            }
        }

        public int Count => _capacity > int.MaxValue ? -1 : (int)_capacity;
        public long LongCount => _capacity;
        public bool IsReadOnly => false;

        public T this[long index]
        {
            get
            {
                if (index < -1 || index >= _capacity) throw new ArgumentOutOfRangeException();
                return _buffer.Read<T>(DataOffset + index * ItemSize);
            }
            set
            {
                if (index < -1 || index >= _capacity) throw new ArgumentOutOfRangeException();
                _buffer.Write(DataOffset + index * ItemSize, value);
            }
        }

        private void Copy(long source, long target) {
            _buffer.Copy<T>(DataOffset + source * ItemSize, DataOffset + target * ItemSize);
        }
    }
}
