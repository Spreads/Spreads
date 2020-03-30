// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Linq;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Utils;

namespace Spreads.Core.Tests.Buffers
{
    [TestFixture]
    public class RecyclableMemoryStreamTests
    {

        [Test, Explicit("long running")]
        public void CouldUseSafeWriteReadByte()
        {
            var doChecks = true;
            var count = doChecks ? 5000 : 1000;
            var size = 1000;
            
            var rng = new Random();
            var bytes = new byte[size];

            var msManager = new Microsoft.IO.RecyclableMemoryStreamManager();

            rng.NextBytes(bytes);
            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("RMS", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var stream = RecyclableMemoryStreamManager.Default.GetStream();

                        for (int j = 0; j < bytes.Length; j++)
                        {
                            stream.SafeWriteByte(bytes[j]);
                        }
                        var p = 0L;
                        int b = 0;
                        while ((b = stream.SafeReadByte(ref p)) >= 0)
                        {
                            if (doChecks && bytes[p - 1] != b)
                            {
                                ThrowHelper.ThrowInvalidOperationException();
                            }
                            p++;
                        }
                        stream.Dispose();
                    }
                }

                using (Benchmark.Run("Original", count * 1000))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var stream = msManager.GetStream() as Microsoft.IO.RecyclableMemoryStream;

                        for (int j = 0; j < bytes.Length; j++)
                        {
                            stream.WriteByte(bytes[j]);
                        }
                        var p = 0;
                        int b = 0;
                        while ((b = stream.SafeReadByte(ref p)) >= 0)
                        {
                            if (bytes[p - 1] != b)
                            {
                                ThrowHelper.ThrowInvalidOperationException();
                            }
                            p++;
                        }
                        stream.Dispose();
                    }
                }
            }

            Benchmark.Dump($"Write/Read single byte, buffer size = {size}, MOPS scaled x1k (read as KOPS)");
        }


        [Test, Explicit("long running")]
        public void CouldUseSafeWriteReadArray()
        {
            var doChecks = false;

            var count = doChecks ? 100 : 100_000;
            var size = 1000;
            

            var rng = new Random();
            var bytes = new byte[size];
            var bytes2 = new byte[size];

            var msManager = new Microsoft.IO.RecyclableMemoryStreamManager();

            rng.NextBytes(bytes);
            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("Spreads.RMS", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var stream = RecyclableMemoryStreamManager.Default.GetStream();
                        stream.SafeWrite(bytes, 0, bytes.Length);
                        var targetPos = 0L;
                        stream.SafeRead(bytes2, 0, bytes2.Length, ref targetPos);
                        if (doChecks && targetPos != bytes.Length)
                        {
                            ThrowHelper.ThrowInvalidOperationException();
                        }
                        if (doChecks)
                        {
                            Assert.True(bytes.SequenceEqual(bytes2));
                        }
                        stream.Dispose();
                    }
                }

                using (Benchmark.Run("Original", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var stream = msManager.GetStream() as Microsoft.IO.RecyclableMemoryStream;
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Dispose();
                    }
                }
            }

            Benchmark.Dump($"Write/Read buffer with size = {size}");
        }
    }
}