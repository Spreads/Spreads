// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class SettingsTests
    {
        [Test, Explicit("Affects and is affected by other tests")]
        public void CouldSetDoAdditionalCorrectnessChecksToTrue()
        {
            Settings.DoAdditionalCorrectnessChecks = false;
            Assert.IsFalse(Settings.DoAdditionalCorrectnessChecks);
        }

        [Test, Explicit("Affects and is affected by other tests")]
        public void CouldSetStaticReadonlyFields()
        {
            Assert.IsTrue(Settings.DoAdditionalCorrectnessChecks);
        }
    }
}
