// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using System.Runtime.CompilerServices;
using Shouldly;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class PaddindTests
    {
        [Test]
        public void SizesAreCorrect()
        {
            Unsafe.SizeOf<Padding16>().ShouldBe(16);
            Unsafe.SizeOf<Padding32>().ShouldBe(32);
            Unsafe.SizeOf<Padding40>().ShouldBe(40);
            Unsafe.SizeOf<Padding48>().ShouldBe(48);
            Unsafe.SizeOf<Padding56>().ShouldBe(56);
            Unsafe.SizeOf<Padding64>().ShouldBe(64);
            Unsafe.SizeOf<Padding112>().ShouldBe(112);
        }
    }
}
