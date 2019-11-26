// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using ObjectLayoutInspector;

namespace Spreads.Core.Tests
{
    [TestFixture]
    public class OptTests
    {
        [Test, Explicit("output")]
        public void OptLayout()
        {
            TypeLayout.PrintLayout<Opt<long>>();
        }

        [Test, Explicit("output")]
        public void NullableLayout()
        {
            TypeLayout.PrintLayout<Opt<long>>();
        }
    }
}
