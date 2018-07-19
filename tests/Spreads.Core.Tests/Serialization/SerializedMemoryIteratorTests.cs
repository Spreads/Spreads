// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Serialization;
using Spreads.Serialization.Utf8Json;
using Spreads.Utils;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class SerializedMemoryIteratorTests
    {
        public Random Random { get; set; } = new Random();

        [Test]
        public void CouldIterateOverSerializedJson()
        {
            var count = 1_00_000;

            var values = new double[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = Random.NextDouble();
            }

            var bytes = JsonSerializer.Serialize(values);

            using (Benchmark.Run("SerializedMemoryIterator JSON", count))
            {
                var iterator = new SerializedMemoryIterator(bytes, false);

                var cnt = 0;
                foreach (var db in iterator)
                {
                    var value = JsonSerializer.Deserialize<double>(db.Span.ToArray());
                    if (Math.Abs(values[cnt] - value) > 0.000000001)
                    {
                        Assert.Fail("Values are not equal");
                    }
                    cnt++;
                }
            }
            Benchmark.Dump();
        }
    }
}