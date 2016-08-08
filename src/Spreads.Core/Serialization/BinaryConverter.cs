//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Spreads.Buffers;

//namespace Spreads.Serialization {

//    public abstract class BinaryConverter<T> : IBinaryConverter<T> {
//        public bool IsFixedSize => Size > 0;
//        public abstract int Size { get; }
//        public abstract int Version { get; }

//        public abstract int SizeOf(T value, out MemoryStream memoryStream);

//        public abstract int Write(T value, IntPtr ptr, MemoryStream memoryStream = null);

//        public abstract int Read(IntPtr ptr, ref T value);


//        public virtual int SizeOf(T[] valueArray, out MemoryStream memoryStream) {
//            if (valueArray == null || valueArray.Length == 0) {
//                memoryStream = null;
//                return 8;
//            }
//            if (Size > 0) {
//                memoryStream = null;
//                // NB individual items are not length-prefixed for fixed-size case
//                return 8 + valueArray.Length * Size;
//            }
//            MemoryStream tempMs;
//            // if there is no intermediate MS for the first element, assume
//            // the same for other elements (and ignore any following MS with a Trace warning)
//            // do not serialize values, just apply SizeOf to each
//            var size = SizeOf(valueArray[0], out tempMs);
//            var totalSize = size + 4;
//            if (tempMs == null) {
//                for (int i = 1; i < valueArray.Length; i++) {
//                    MemoryStream tempMs2;
//                    // length-prefixed payloads
//                    totalSize += 4 + SizeOf(valueArray[i], out tempMs2);
//                    if (tempMs2 == null) continue;
//                    Trace.TraceWarning($"Inconsistent SizeOf behavior w.r.t. memory stream for the type {typeof(T).FullName}");
//                    tempMs2.Dispose();
//                }
//                memoryStream = null;
//                return 8 + totalSize; // 8 for version + length header
//            }

//            // have to accumulate actual serialized data into a memory stream
//            memoryStream = RecyclableMemoryManager.MemoryStreams.GetStream();
//            Debug.Assert(memoryStream.Position == 0);
//            // version + flags
//            memoryStream.WriteAsPtr<int>(0);
//            // size placeholder
//            memoryStream.WriteAsPtr<int>(0);
//            Debug.Assert(memoryStream.Position == 8);

//            // first element size
//            memoryStream.WriteAsPtr<int>(size);
//            // first payload
//            tempMs.CopyTo(memoryStream);

//            for (int i = 1; i < valueArray.Length; i++) {
//                MemoryStream tempMs2;
//                size += SizeOf(valueArray[i], out tempMs2);
//                if (tempMs2 != null) Trace.TraceWarning($"Inconsistent SizeOf behavior w.r.t. memory stream for the type {typeof(T).FullName}");
//                tempMs2?.Dispose();
//            }

//            foreach (var item in valueArray) {
//                var buffer = RecyclableMemoryManager.GetBuffer(size);
//                // NB do not use a buffer pool here but instead use a thread-static buffer
//                // that will grow to maximum size of a type. Fixed-size types are usually small.
//                // Take/return is more expensive than the work we do with the pool here.
//                if (RecyclableMemoryManager.ThreadStaticBuffer == null || BinaryConverterExtensions.ThreadStaticBuffer.Length < size) {
//                    BinaryConverterExtensions.ThreadStaticBuffer = new byte[size];
//                    if (size > MaxBufferSize) {
//                        // NB 8 kb is arbitrary
//                        Trace.TraceWarning("Thread-static buffer in BinaryConverterExtensions is above 8kb");
//                    }
//                }
//                fixed (byte* ptr = &_threadStaticBuffer[0]) {
//                    TypeHelper<T>.Write(value, (IntPtr)ptr);
//                }
//                stream.Write(_threadStaticBuffer, 0, size);

//                // NB this is not needed as long as converter.Write guarantees overwriting all Size bytes.
//                // //Array.Clear(_buffer, 0, size);
//                return size;
//            }
//            memoryStream.Position = 0;
//        }
//    }
//}
