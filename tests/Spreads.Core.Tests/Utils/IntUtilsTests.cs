// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using Shouldly;

namespace Spreads.Core.Tests.Utils
{
    [TestFixture]
    public class IntUtilsTests
    {
        [Test]
        public void LzcntNegative()
        {
            BitUtils.LeadingZeroCount(-1).ShouldBe(0);
            Console.WriteLine(BitUtils.LeadingZeroCount(-1));
        }
    }
}
