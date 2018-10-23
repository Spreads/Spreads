// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class RetainedMemoryTests
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RetainedMemory
        {
            internal Memory<byte> _memory;

            // Could add Deconstruct method
            internal MemoryHandle _memoryHandle;
            private int _offset;
        }

        [Test]
        public void SizeOfMemoryStructs()
        {
            Console.WriteLine("Memory: " +  Unsafe.SizeOf<Memory<byte>>());
            Console.WriteLine("MemoryHandle: " +  Unsafe.SizeOf<MemoryHandle>());
            Console.WriteLine("RetainedMemory: " +  Unsafe.SizeOf<RetainedMemory<byte>>());
            Console.WriteLine("RetainedMemory NonGeneric: " +  Unsafe.SizeOf<RetainedMemory>());
        }

        [Test]
        public void CouldUseRetainedmemory()
        {
            var bytes = new byte[100];
            var rm = BufferPool.Retain(123456, true);
            // var ptr = rm.Pointer;
            var mem = rm.Memory;
            var clone = rm.Clone();
            var clone1 = rm.Clone();
            var clone2 = clone.Clone();
            rm.Dispose();
            clone.Dispose();
            clone2.Dispose();
            
            var b = mem.Span[0];

            clone1.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var b2 = mem.Span[0];
            });

        }

        [Test]
        public void CouldCreateRetainedmemoryFromArray()
        {
            var array = new byte[100];
            var rm = new RetainedMemory<byte>(array);

            var rmc = rm.Clone();

            rm.Dispose();

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            rmc.Span[1] = 1;
            
            // Assert.AreEqual(1, array[1]);
        }

        //public class RMContent : HttpContent
        //{
        //    public RMContent()
        //    {
        //        ReadOnlySequence<byte> buffer = default;
        //        buffer.TryGet()
        //        _content = content;
        //        if (MemoryMarshal.TryGetArray(content, out ArraySegment<byte> array))
        //        {
        //            // If we have an array, allow HttpClient to take optimized paths by just
        //            // giving it the array content to use as its already buffered data.
        //            SetBuffer(array.Array, array.Offset, array.Count);
        //        }
        //    }

        //    protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        //    {
        //        throw new NotImplementedException();
        //    }

        //    protected override bool TryComputeLength(out long length)
        //    {
        //        throw new NotImplementedException();
        //    }
        //}

        [Test]
        public async Task CouldReadStreamToRetainedMemory()
        {
            var rms = RecyclableMemoryStream.Create();
            for (int i = 0; i < 100; i++)
            {
                rms.WriteByte((byte)i);
            }

            rms.Position = 0;

            var rm = await rms.ToRetainedMemory(1);

            Assert.AreEqual(100, rm.Length);

            rms.Position = 0;

            rm = await rms.ToRetainedMemory(100);

            Assert.AreEqual(100, rm.Length);

            rms.Position = 0;
            var nss = new NonSeekableStream(rms);

            rm = await nss.ToRetainedMemory(1);

            Assert.AreEqual(100, rm.Length);

            nss.Position = 0;

            rm = await nss.ToRetainedMemory(100);

            Assert.AreEqual(100, rm.Length);
        }

        [Test]
        public unsafe void TrimUpdatesPointer()
        {
            var rm = BufferPool.Shared.RetainMemory(1000);

            var initialPtr = (IntPtr)rm.Pointer;

            rm.Trim(1, 100);

            Assert.AreEqual(initialPtr.ToInt64() + 1, ((IntPtr)rm.Pointer).ToInt64());


            rm = BufferPool.OffHeapMemoryPool.RetainMemory(1000);

            initialPtr = (IntPtr)rm.Pointer;

            rm.Trim(1, 100);

            Assert.AreEqual(initialPtr.ToInt64() + 1, ((IntPtr)rm.Pointer).ToInt64());

        }
    }


    public class NonSeekableStream : Stream
    {
        Stream _stream;
        public NonSeekableStream(Stream baseStream)
        {
            _stream = baseStream;
        }
#if NETCOREAPP2_1
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.ReadAsync(buffer, cancellationToken);
        }
#endif

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public override bool CanRead => _stream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _stream.CanWrite;

        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
    }
}