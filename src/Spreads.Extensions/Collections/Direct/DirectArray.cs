using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using Spreads.Serialization;

namespace Spreads.Collections.Direct {

    internal class DirectArray<T> : IEnumerable<T>, IDisposable where T : struct
    {
        private static int counter = 0;
        private static object SyncRoot = new object();
        internal const int HeaderLength = 256;
        internal static int DataOffset = HeaderLength + TypeHelper<T>.Size;
        private readonly string _filename;
        internal long _capacity;
        public static readonly int ItemSize;
        private MemoryMappedFile _mmf;
        private FileStream _fileStream;
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
            ItemSize = TypeHelper<T>.Size;
        }

        private DirectArray(string filename, long minCapacity, T fill) {
            _filename = filename;

            Grow(minCapacity);
        }

        public DirectArray(string filename, long minCapacity = 5L) : this(filename, minCapacity, default(T)) {

        }

        internal void Grow(long minCapacity) {
            lock (SyncRoot) {
                _fileStream?.Dispose();
                _fileStream = new FileStream(_filename, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.ReadWrite, 4096,
                    FileOptions.Asynchronous | FileOptions.RandomAccess);

                _capacity = (_fileStream.Length - DataOffset) / ItemSize;
                var newCapacity = Math.Max(_capacity, minCapacity);

                long bytesCapacity = DataOffset + newCapacity * ItemSize;
                _capacity = newCapacity;
                var sec = new MemoryMappedFileSecurity();
                _mmf?.Dispose();
                var unique = ((long) Process.GetCurrentProcess().Id << 32) | (long)counter++;
                var mmf = MemoryMappedFile.CreateFromFile(_fileStream,
                    $@"{Path.GetFileName(_filename)}.{unique}", bytesCapacity, 
                    MemoryMappedFileAccess.ReadWrite, sec, HandleInheritability.Inheritable,
                    false);
                _mmf = mmf;

                unsafe
                {
                    byte* ptr = (byte*)0;
                    var va = _mmf.CreateViewAccessor(0, bytesCapacity, MemoryMappedFileAccess.ReadWrite);

                    var sh = va.SafeMemoryMappedViewHandle;
                    sh.AcquirePointer(ref ptr);
                    var ptrV = new IntPtr(ptr);
                    _buffer = new DirectBuffer(bytesCapacity, ptrV);
                    va.Dispose();
                }
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

        internal DirectBuffer Buffer => _buffer;

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
