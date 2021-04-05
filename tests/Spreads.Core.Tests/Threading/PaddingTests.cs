// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Threading;
using System.Runtime.CompilerServices;

namespace Spreads.Core.Tests.Threading
{
    [TestFixture]
    public class PaddindTests
    {
        [Test]
        public void SizesAreCorrect()
        {
            Assert.AreEqual(16, Unsafe.SizeOf<Padding16>());
            Assert.AreEqual(32, Unsafe.SizeOf<Padding32>());
            Assert.AreEqual(40, Unsafe.SizeOf<Padding40>());
            Assert.AreEqual(48, Unsafe.SizeOf<Padding48>());
            Assert.AreEqual(56, Unsafe.SizeOf<Padding56>());
            Assert.AreEqual(64, Unsafe.SizeOf<Padding64>());
            Assert.AreEqual(112, Unsafe.SizeOf<Padding112>());
        }
    }
}