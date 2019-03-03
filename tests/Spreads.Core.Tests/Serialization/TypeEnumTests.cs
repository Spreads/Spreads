// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization.Experimental;
using Spreads.Utils;
using System;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class TypeEnumTests
    {
        [Test]
        public void CouldGetUnknownFixedSize()
        {
            for (int i = 1; i <= 128; i++)
            {
                var te = new TypeEnumOrFixedSize((byte)i);
                Assert.AreEqual(i, te.Size);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new TypeEnumOrFixedSize((byte)0);
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var _ = new TypeEnumOrFixedSize((byte)129);
            });
        }


        [Test, Explicit("bench")]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void SizeGetterBench()
        {
            var count = 2_000_000L;
            var sum = 0L;
            for (int r = 0; r < 20; r++)
            {
                using (Benchmark.Run("Size Get", count * 255))
                {
                    for (int _ = 0; _ < count; _++)
                    {
                        for (byte b = 0; b < 255; b++)
                        {
                            sum += TypeEnumOrFixedSize.GetSize(b);
                        }
                    }
                }
            }

            Benchmark.Dump();
        }
    }
}