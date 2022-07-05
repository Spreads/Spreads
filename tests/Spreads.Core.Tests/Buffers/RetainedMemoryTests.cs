﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Shouldly;

namespace Spreads.Core.Tests.Buffers
{
    [Category("CI")]
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
            Console.WriteLine("Memory: " + Unsafe.SizeOf<Memory<byte>>());
            Console.WriteLine("MemoryHandle: " + Unsafe.SizeOf<MemoryHandle>());
            Console.WriteLine("RetainedMemory: " + Unsafe.SizeOf<RetainedMemory<byte>>());
            Console.WriteLine("RetainedMemory NonGeneric: " + Unsafe.SizeOf<RetainedMemory>());
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
            // TODO caching Memory as a field now makes this fail, we could touch disposed memory
            // Assert.Throws<ObjectDisposedException>(() =>
            // {
            //     var b2 = mem.Span[0];
            // });
        }

        [Test]
        public void CouldCreateRetainedMemoryFromArray()
        {
            var array = new byte[100];
            using var rm = new RetainedMemory<byte>(array);

            using var rmc = rm.Clone(10);

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            rmc.GetSpan()[1] = 1;

            ((int)array[11]).ShouldBe(1);
        }

        [Test]
        public void OffsetsAreOk()
        {
            var array = new byte[100];
            using var rm = new RetainedMemory<byte>(ArrayMemory<byte>.Create(array, 50, 50, true), 25, 25, true);

            // 85 - 95
            using var rmc = rm.Clone(10, 10);

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            rmc.GetSpan()[5] = 1;

            ((int)array[90]).ShouldBe(1);
        }

        [Test]
        public unsafe void TrimUpdatesPointer()
        {
            var rm = BufferPool.Retain(1000);

            var initialPtr = (IntPtr)rm.Pointer;

            var rm1 = rm.Clone(1, 100);

            ((IntPtr)rm1.Pointer).ToInt64().ShouldBe(initialPtr.ToInt64() + 1);

            rm1.Dispose();
            rm.Dispose();

            rm = BufferPool.Retain(1000);

            initialPtr = (IntPtr)rm.Pointer;

            rm1 = rm.Clone(1, 100);

            ((IntPtr)rm1.Pointer).ToInt64().ShouldBe(initialPtr.ToInt64() + 1);
            rm1.Dispose();
            rm.Dispose();
        }

        [Test]
        public void WorksWithNonBlittables()
        {
            var arr = new string[] { "a" };
            var r = ArrayMemory<string>.Create(arr, 0, arr.Length, externallyOwned: true);

            var rm0 = r.Retain();

            r.IsPinned.ShouldBe(false);

            var rm1 = r.Retain();

            rm1.Dispose();

            r.ReferenceCount.ShouldBe(1);

            var vec = r.GetVec();
            vec.Length.ShouldBe(1);
            vec[0].ShouldBe("a");
            var rm = r.Retain();

            r.ReferenceCount.ShouldBe(2);

            rm.GetSpan()[0].ShouldBe("a");
            rm.Dispose();
            r.ReferenceCount.ShouldBe(1);

            rm0.Dispose();
        }
    }

    public class NonSeekableStream : Stream
    {
        private Stream _stream;

        public NonSeekableStream(Stream baseStream)
        {
            _stream = baseStream;
        }

#if NETCOREAPP2_1xxx

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
