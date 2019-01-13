// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using NUnit.Framework;
using Spreads.Collections.Generic;
using Spreads.Serialization;

namespace Spreads.Core.Tests.Collections
{
    [TestFixture]
    public unsafe class VecTests
    {
        [Test]
        public void HelperFirstElemOffsetWorks()
        {
            var offset = TypeHelper<int>.ElemOffset;
            Assert.IsTrue(offset > 0);
            Console.WriteLine(offset);
        }
    }
}
