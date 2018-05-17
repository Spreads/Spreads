// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Spreads.Buffers;
using Spreads.Serialization;
using Spreads.Storage;

namespace Spreads.Core.Tests {
    [TestFixture]
    public class DirectFileTests {

        [Test]
        public unsafe void CouldCatchErrorWhileWritingPastBoundary()
        {
            // This doesn;t throw unless size is 4096 vs 12
            // Could not make any assumptions that it is safe to write past boundary
            // with try..catch. Even byte[] allows it and we probably corrupt 
            // .NETs memory next to it.
            var bytes = new byte[12];
            var fb = new FixedBuffer(bytes);
            fixed (byte* ptr = &bytes[10])
            {
                *(long*)(ptr) = long.MaxValue;
            }

            var df = new DirectFile("../CouldCatchErrorWhileWritingPastBoundary", 12);
            *(long*)(df.Buffer.Data + 10) = long.MaxValue;
            df.Dispose();
            var df2 = new DirectFile("../CouldCatchErrorWhileWritingPastBoundary", 12);
            Assert.AreEqual(long.MaxValue, *(long*)(df2.Buffer.Data + 10));
        }
    }
}
