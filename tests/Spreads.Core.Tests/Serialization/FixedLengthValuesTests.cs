// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public unsafe class FixedLengthValuesTests
    {
        [Test, Explicit("long running")]
        public void CouldSerializeInts()
        {
            var bytes = new byte[(4 + 4) * 100_000_000];
            var mem = (Memory<byte>)bytes;
            using (Benchmark.Run("Int serialization", bytes.Length / 8))
            {
                fixed (byte* ptr = &bytes[0])
                {
                    for (int i = 0; i < bytes.Length / 8; i++)
                    {
                        var slice = mem.Slice(i * 8);
                        BinarySerializer.Write(i, mem.Slice(i * 8), default, SerializationFormat.Binary);
                        BinarySerializer.Read(slice, out int j);
                        if (i != j)
                        {
                            Assert.Fail();
                        }
                    }
                }
            }
        }

        [Test, Explicit("long running")]
        public void CouldWriteInts()
        {
            var bytes = new byte[(4 + 4) * 200_000_000];
            using (Benchmark.Run("Int serialization", bytes.Length / 8))
            {
                fixed (byte* ptr = &bytes[0])
                {
                    var x = bytes.Length / 8;
                    for (int i = 0; i < x; i++)
                    {
                        *(int*)(ptr + i * 8) = i;
                        var j = *(int*)(ptr + i * 8);
                        //System.Runtime.CompilerServices.Unsafe.Write((void*)(ptr + i * 8), i);
                        //var j = System.Runtime.CompilerServices.Unsafe.Read<int>((void*) (ptr + i * 8));
                        if (i != j)
                        {
                            Assert.Fail();
                        }
                    }
                }
            }
        }
    }
}
