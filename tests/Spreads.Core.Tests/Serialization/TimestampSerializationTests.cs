// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;
using Spreads.Buffers;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public unsafe class TimeStampSerializationTests
    {
        [Test, Explicit("long running")]
        public void TestTimestampBranchlessRead()
        {
            var count = 10_000_000;
            var rounds = 10;
            var rm = BufferPool.Retain(16 * count, true);
            var db = new DirectBuffer(rm);
            

            long tsSum = 0;
            var rng = new Random(42);
            for (int i = 0; i < count; i++)
            {
                var ptrI = db.Slice( i * 16);
                var x = rng.Next(0, 100) / 100.0;
                var isTimestamped = x < 0.5; // This is perfectly predicted: (i & 1) == 0;
                Timestamp ts = isTimestamped ? default : (Timestamp)i;
                tsSum += (long)ts;
                BinarySerializer.Write(in i, ref ptrI, timestamp: ts);
            }

            for (int r = 0; r < rounds; r++)
            {
                long tsSumIf = 0;
                using (Benchmark.Run("IF", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var ptrI = db.Slice(i * 16);
                        var header = ptrI.Read<DataTypeHeader>(0);
                        if (header.VersionAndFlags.IsTimestamped && header.IsTypeFixedSize)
                        {
                            var ts = ptrI.Read<Timestamp>(4);
                            unchecked
                            {
                                tsSumIf += (long)ts;
                            }
                        }
                    }
                    Assert.AreEqual(tsSum, tsSumIf);
                }

                long tsSumBrl = 0;
                using (Benchmark.Run("BRL", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var ptrI = db.Slice(i * 16);

                        var ts = BinarySerializer.ReadTimestamp2((byte*)ptrI.Data, out _);
                        unchecked
                        {
                            tsSumBrl += (long)ts;
                        }
                    }
                    Assert.AreEqual(tsSum, tsSumBrl);
                }
            }

            Benchmark.Dump();

            Console.WriteLine(tsSum);
        }

        [Test, Explicit("long running")]
        public void TestTimestampBranchlessRead2()
        {
            var count = 10_000_000;
            var itemMaxSize = 20;
            var rounds = 10;
            var ptr = (byte*)Marshal.AllocHGlobal(itemMaxSize * count);

            long tsSum = 0;
            long valSum = 0;
            var rng = new Random(42);
            for (int i = 0; i < count; i++)
            {
                var ptrI = ptr + i * itemMaxSize;
                var x = rng.Next(0, 100) / 100.0;

                var fixedHeader = new DataTypeHeader
                {
                    VersionAndFlags = new VersionAndFlags()
                    {
                        IsTimestamped = true,
                    },
                    TypeSize = 4,
                    TypeEnum = TypeEnum.Int32
                };

                var varSizeHeader = new DataTypeHeader
                {
                    VersionAndFlags = new VersionAndFlags()
                    {
                        IsTimestamped = true,
                    },
                    TypeSize = 0,
                    TypeEnum = TypeEnum.Int32
                };

                Timestamp ts = (Timestamp)i;
                tsSum += (long)ts;
                valSum += i;

                var isFixedSize = x < 0.5; // This is perfectly predicted: (i & 1) == 0;
                if (isFixedSize)
                {
                    *(DataTypeHeader*)ptrI = fixedHeader;
                    *(Timestamp*)(ptrI + 4) = ts;
                    *(int*)(ptrI + 12) = i;
                }
                else
                {
                    *(DataTypeHeader*)ptrI = varSizeHeader;
                    *(int*)(ptrI + 4) = 4;
                    *(Timestamp*)(ptrI + 8) = ts;
                    *(int*)(ptrI + 16) = i;
                }
            }

            for (int r = 0; r < rounds; r++)
            {
                long tsSumIf = 0;
                long valSumIf = 0;
                using (Benchmark.Run("IF", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var ptrI = ptr + i * itemMaxSize;
                        var header = *(DataTypeHeader*)ptrI;
                        if (header.VersionAndFlags.IsTimestamped)
                        {
                            unchecked
                            {
                                if (header.IsTypeFixedSize)
                                {
                                    var ts = *(Timestamp*)(ptrI + 4);
                                    tsSumIf += (long)ts;
                                    valSumIf += *(int*)(ptrI + 12);
                                }
                                else
                                {
                                    var ts = *(Timestamp*)(ptrI + 8);
                                    tsSumIf += (long)ts;
                                    valSumIf += *(int*)(ptrI + 16);
                                }
                            }
                        }
                    }
                    Assert.AreEqual(tsSum, tsSumIf, "1");
                    Assert.AreEqual(valSum, valSumIf, "2");
                }

                long tsSumBrl = 0;
                long valSumBrl = 0;
                using (Benchmark.Run("BRL", count))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var ptrI = ptr + i * itemMaxSize;

                        var ts = BinarySerializer.ReadTimestamp2(ptrI, out var offset);
                        var val = *(int*)(ptrI + offset);
                        unchecked
                        {
                            valSumBrl += val;
                            tsSumBrl += (long)ts;
                        }
                    }
                    Assert.AreEqual(tsSum, tsSumBrl, "3");
                    Assert.AreEqual(valSum, valSumBrl, "4");
                }
            }

            Benchmark.Dump();

            Console.WriteLine(tsSum);
        }
    }
}