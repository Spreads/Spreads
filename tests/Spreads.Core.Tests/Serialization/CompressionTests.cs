// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;
using Spreads.Threading;

namespace Spreads.Core.Tests.Serialization
{
    [Category("CI")]
    [TestFixture]
    public class CompressionTests
    {
        [Test]
        public void CouldCompressWithHeader()
        {
            CouldCompressWithHeader(17);
        }

        [Test, Explicit("long-running")]
        public void CouldCompressWithHeaderManyTimes()
        {
            // TODO run this for several hours, catch any error and log problematic seed if any
            var rounds = 100;
            for (int i = 0; i < rounds; i++)
            {
                try
                {
                    CouldCompressWithHeader(i, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with seed: {i}, ex: {ex}");
                    throw;
                }
            }
        }

        public void CouldCompressWithHeader(int seed, bool silent = false)
        {
            var rm = BufferPool.Retain(1000000);
            var db = new DirectBuffer(rm.Span);

            var rm1 = BufferPool.Retain(1000000);
            var db1 = new DirectBuffer(rm1.Span);

            var rm2 = BufferPool.Retain(1000000);
            var db2 = new DirectBuffer(rm2.Span);

            var count = 1000;

            var rng = new Random(seed);

            var values = new TestValue[count];

            var dest = db;
            var payloadSize = 0;
            for (int i = 0; i < count; i++)
            {
                var ts = TimeService.Default.CurrentTime;

                var value = TestValue.Create(rng, false); // TODO with true, currently doubles are not equal in some cases, se  upstream issue 83
                values[i] = value;
                var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Json);

                var written = BinarySerializer.Write(in value, dest, tmpBuffer, SerializationFormat.Json);

                if (size != written)
                {
                    Assert.Fail($"size {size} != written {written}");
                }

                dest = dest.Slice(written);

                payloadSize += written;
            }

            if (!silent) { Console.WriteLine($"Payload size: {payloadSize}"); }

            //Settings.ZlibCompressionLevel = 9;
            //Settings.LZ4CompressionLevel = 9;
            //Settings.ZstdCompressionLevel = 9;

            foreach (var method in new[] { CompressionMethod.GZip, CompressionMethod.Lz4, CompressionMethod.Zstd })
            {
                var compressed = BinarySerializer.Compress(db.Slice(0, payloadSize).Span, db1.Span, method);
                if (!silent)
                {
                    Console.WriteLine(
                        $"{method}: compressed size: {compressed}, ratio: {Math.Round(payloadSize / (compressed * 1.0), 2)}");
                }

                var decomrpessed = BinarySerializer.Decompress(db1.Slice(0, compressed).Span, db2.Span, method);

                if (decomrpessed != payloadSize)
                {
                    Assert.Fail($"decomrpessed {decomrpessed} != payloadSize {payloadSize}");
                }

                for (int i = 0; i < count; i++)
                {
                    var read = BinarySerializer.Read(db2, out TestValue value1);
                    db2 = db2.Slice(read);
                    var expected = values[i];
                    if (!value1.Equals(expected))
                    {
                        Assert.Fail($"value1 != values[i]");
                    }
                }
            }

            rm.Dispose();
            rm1.Dispose();
            rm2.Dispose();
        }

        [Test
#if !DEBUG
         , Explicit("long-running")
#endif
        ]
        public void CouldPackWithHeaderBenchmark()
        {
            Settings.DoAdditionalCorrectnessChecks = false;

            var silent = true;

            var rm = BufferPool.Retain(100_000_000);
            var db = new DirectBuffer(rm.Span);
#if !DEBUG
            var count = 200_000;
#else
            var count = 200;
#endif
            var rounds = 10;

            var rng = new Random(0);

            var values = new TestValue[count];

            for (int i = 0; i < count; i++)
            {
                var value = TestValue.Create(rng, false); // TODO with true, currently doubles are not equal in some cases, se  upstream issue 83
                values[i] = value;
            }

            var dest = db;
            int payloadSize = 0;

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Write", count))
                {
                    dest = db;
                    payloadSize = 0;
                    for (int i = 0; i < count; i++)
                    {
                        var ts = TimeService.Default.CurrentTime;

                        var value = values[i];
                        var size = BinarySerializer.SizeOf(in value, out var tmpBuffer, SerializationFormat.Json);

                        var written = BinarySerializer.Write(in value, dest, tmpBuffer, SerializationFormat.Json);

                        if (size != written)
                        {
                            Assert.Fail($"size {size} != written {written}");
                        }

                        dest = dest.Slice(written);

                        payloadSize += written;
                    }
                }
            }

            for (int r = 0; r < rounds; r++)
            {
                using (Benchmark.Run("Read", count))
                {
                    var readSource = db.Slice(0, payloadSize);

                    for (int i = 0; i < count; i++)
                    {
                        var read = BinarySerializer.Read(readSource, out TestValue value1);
                        readSource = readSource.Slice(read);
                        var expected = values[i];
                        if (!value1.Equals(expected))
                        {
                            Assert.Fail($"value1 != values[i]");
                        }
                    }
                }
            }

            Benchmark.Dump();

            rm.Dispose();
        }
    }
}