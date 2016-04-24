using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Collections {

    public class DirectArray<T> : IList<T>, IDisposable where T : struct {
        private readonly string _filename;
        private long _capacity;
        private static int ItemSize;
        private MemoryMappedFile _mmf;
        private FileStream _fileStream;
        private DirectBuffer _buffer;
        private SafeBuffer _safeBuffer;

        static DirectArray() {
            // start with only blittables as POC
            ItemSize = Marshal.SizeOf(typeof(T));
        }

        private static long IdxToOffset(long idx) {
            return idx * ItemSize;
        }
        private static long OffsetToIdx(long offset) {
            return offset / ItemSize;
        }

        public DirectArray(string filename, long capacity = 4L) {
            _filename = filename;
            
            _fileStream = new FileStream(_filename, FileMode.OpenOrCreate,
                            FileAccess.ReadWrite, FileShare.ReadWrite, 8192,
                            FileOptions.Asynchronous | FileOptions.RandomAccess);

            Grow(capacity);
        }

        public void Grow(long newCapacity)
        {
            if (newCapacity <= _capacity) return;
            _capacity = newCapacity;
            var bytesCapacity = Math.Max(_fileStream.Length, IdxToOffset(newCapacity));
            var mmfs = new MemoryMappedFileSecurity();
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
                _buffer.VolatileWriteInt64(42, 24);
            }
            _safeBuffer = _mmf.CreateViewAccessor().SafeMemoryMappedViewHandle; //_buffer.CreateSafeBuffer();

        }

        public void Dispose() {
            _mmf.Dispose();
            _fileStream.Close();
        }

        public IEnumerator<T> GetEnumerator() {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(T item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(T item) {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(T item) {
            throw new NotImplementedException();
        }

        public int Count => _capacity > int.MaxValue ? -1 : (int)_capacity;
        public long LongCount => _capacity;
        public bool IsReadOnly => false;
        public int IndexOf(T item) {
            throw new NotImplementedException();
        }

        public void Insert(int index, T item) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }

        public T this[int index]
        {
            get
            {
                T temp;
                //_safeBuffer.Read<T>(IdxToOffset(index), out temp);
                //return temp;
                return _safeBuffer.Read<T>((ulong)IdxToOffset(index));
            }
            set
            {
                //_buffer.WriteInt64((int)IdxToOffset(index), Convert.ToInt64(value));
                _buffer.Write<T>((int)IdxToOffset(index), value);
                //_safeBuffer.Write<T>(IdxToOffset(index), ref value);
                //_safeBuffer.Write<T>((ulong)IdxToOffset(index), value);
            }
        }
    }
}
