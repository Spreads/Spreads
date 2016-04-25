using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Spreads.Serialization;

namespace Spreads.Collections {

    public class DirectArray<T> : IEnumerable<T>, IDisposable where T : struct {
        private const int HeaderOffset = 256;
        private readonly string _filename;
        private long _capacity;
        private static int ItemSize;
        private MemoryMappedFile _mmf;
        private readonly FileStream _fileStream;
        private DirectBuffer _buffer;

        // MMaped pointers in the header for custom use
        internal IntPtr Slot0 => _buffer.Data;
        internal IntPtr Slot1 => _buffer.Data + 8;
        internal IntPtr Slot2 => _buffer.Data + 16;
        internal IntPtr Slot3 => _buffer.Data + 24;
        internal IntPtr Slot4 => _buffer.Data + 32;
        internal IntPtr Slot5 => _buffer.Data + 40;
        internal IntPtr Slot6 => _buffer.Data + 48;
        internal IntPtr Slot7 => _buffer.Data + 56;

        static DirectArray() {
            // start with only blittables as POC
            ItemSize = TypeHelper<T>.Size; // Marshal.SizeOf(typeof(T));
        }

        private static long IdxToOffset(long idx) {
            return idx * ItemSize;
        }

        private static long OffsetToIdx(long offset) {
            return offset / ItemSize;
        }

        public DirectArray(string filename, long capacity = 4L) {
            _filename = filename;
            _fileStream = new FileStream(_filename, FileMode.Truncate,
                            FileAccess.ReadWrite, FileShare.ReadWrite, 4096,
                            //FileOptions.Asynchronous | FileOptions.RandomAccess);
                            FileOptions.None);
            Grow(capacity);
        }

        public void Grow(long newCapacity) {
            if (newCapacity <= _capacity) return;
            _capacity = newCapacity;
            var mmfs = new MemoryMappedFileSecurity();
            
            var bytesCapacity = HeaderOffset + Math.Max(_fileStream.Length, IdxToOffset(newCapacity));
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

        private IEnumerable<T> AsEnumerable()
        {
            for (int i = 0; i < _capacity; i++)
            {
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
            for (int i = 0; i < _capacity; i++)
            {
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
                return _buffer.Read<T>(HeaderOffset + IdxToOffset(index));
            }
            set
            {
                _buffer.Write(HeaderOffset + IdxToOffset(index), value);
            }
        }
    }
}
