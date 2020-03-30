// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class SettingsTests
    {
        [Test, Explicit("Affects and is affected by other tests")]
        public void CouldSetDoAdditionalCorrectnessChecksToTrue()
        {
            Settings.DoAdditionalCorrectnessChecks = false;
            #if DEBUG
            Assert.IsTrue(Settings.DoAdditionalCorrectnessChecks);
            #else
            Assert.IsFalse(Settings.DoAdditionalCorrectnessChecks);
            #endif
        }

        [Test, Explicit("Affects and is affected by other tests")]
        public void CouldSetStaticReadonlyFields()
        {
            Assert.IsTrue(Settings.DoAdditionalCorrectnessChecks);
        }
    }
}
