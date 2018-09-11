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
    public class BrotliTests
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


    }
}