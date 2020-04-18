// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Internal;

#pragma warning disable 618

namespace Spreads.Core.Tests.Collections.Internal
{
    [Category("CI")]
    [TestFixture]
    public class DataBlockTreeTests
    {
        [Test]
        public void CouldAppend()
        {
            DataBlock.MaxLeafSize = 2;
            DataBlock.MaxNodeSize = 2;

            var db = DataBlock.CreateForSeries<int, long>();

            DataBlock.Append<int, long>(db, 1, 1, out var blockToAdd);

            db.RowCount.ShouldEqual(1);
        }
    }
}