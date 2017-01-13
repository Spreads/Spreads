// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Experimental.Utils.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Experimental.Core.Tests {

    [TestFixture]
    public class AwaiterTests {

        [Test, Ignore]
        public void MultipleWaiters() {
            var awaitabe = new ManualResetAwaitable();

            var t = Task.Run(async () => {
                var result = await awaitabe;
                Assert.AreEqual(true, result);
            });
            var t2 = Task.Run(async () => {
                var result = await awaitabe;
                Assert.AreEqual(true, result);
            });

            var t3 = Task.Run(async () => {
                var result = await awaitabe;
                Assert.AreEqual(true, result);
            });

            Thread.Sleep(500);
            awaitabe.Awaiter.SignalResult();

            t.Wait();
            t2.Wait();
            t3.Wait();
        }
    }
}
