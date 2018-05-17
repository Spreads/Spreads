// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using BufferPool = Spreads.Buffers.BufferPool;

namespace Spreads.Tests.Serialization
{
    [TestFixture]
    public class VariableLengthValuesTests
    {
        [Test, Ignore("long running")]
        public void CouldCreateBinaryConverter()
        {
            var converter = GetConverter<PreservedBuffer<byte>[]>();

            Assert.AreEqual(0, converter.Size);
            var buffers = new[] { BufferPool.PreserveMemory(10) };
            //Assert.Throws<NotImplementedException>(() =>
            //{
            converter.SizeOf(buffers, 0, 10, out var temp);
            //});
        }

        private static ICompressedArrayBinaryConverter<T> GetConverter<T>()
        {
            return PreservedBufferArrayBinaryConverterFactory<T>.Instance;
        }
    }
}