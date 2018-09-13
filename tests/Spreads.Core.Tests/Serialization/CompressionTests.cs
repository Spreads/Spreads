// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Utils;
using System;
using System.Runtime.InteropServices;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class CompressionTests
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct TestValue
        {
            public string Str { get; set; }
            public string Str1 { get; set; }
            public int Num { get; set; }
            public int Num1 { get; set; }
            public int Num2 { get; set; }

            // public Decimal Dec { get; set; }

            public double Dbl { get; set; }
            public double Dbl1 { get; set; }

            public bool Boo { get; set; }
        }

        [Test]
        public void CouldCompressWithHeader()
        {
            var rm = BufferPool.Retain(1000000);
            var count = 1000;
            var values = new TestValue[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = new TestValue()
                {
                    // Dec = (((decimal)i + 1M / (decimal)(i + 1))),
                    Dbl = (double)i + 1 / (double)(i + 1),
                    //Dbl1 = (double)i + 1 / (double)(i + 1),
                    Num = i,
                    Num1 = i,
                    Num2 = i,
                    Str = i.ToString(),
                    //Str1 = ((double)i + 1 / (double)(i + 1)).ToString(),
                    Boo = i % 2 == 0
                };
            }


            // Spreads.Serialization.Utf8Json.JsonSerializer.Serialize()

        }

    }
}