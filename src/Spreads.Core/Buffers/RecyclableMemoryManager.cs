using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Spreads.Buffers {


    /// <summary>
    /// A helper class that holds a RecyclableMemoryStreamManager singletone 
    /// and methods to get temporary buffers.
    /// </summary>
    public class RecyclableMemoryManager {
        public const int StaticBufferSize = 16 * 1024;
        [ThreadStatic]
        private static byte[] _threadStaticBuffer;

        internal static byte[] ThreadStaticBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _threadStaticBuffer ?? (_threadStaticBuffer = new byte[StaticBufferSize]); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BufferWrapper<byte> GetBuffer(int minimumSize) {
            if (StaticBufferSize >= minimumSize) {
                return new BufferWrapper<byte>(ThreadStaticBuffer, false);
            }
            var staticBuffer = ArrayPool<byte>.Shared.Rent(minimumSize);
            return new BufferWrapper<byte>(staticBuffer, true);
        }

        public static RecyclableMemoryStreamManager MemoryStreams = new RecyclableMemoryStreamManager();
        private RecyclableMemoryManager() { }
    }
}
