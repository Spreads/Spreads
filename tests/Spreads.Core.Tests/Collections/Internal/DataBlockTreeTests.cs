// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections.Internal;
using Spreads.Native;

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
            var blockLimit = Settings.MIN_POOLED_BUFFER_LEN;
            DataBlock.MaxLeafSize = blockLimit;
            DataBlock.MaxNodeSize = blockLimit;

            var db = DataBlock.CreateSeries<int, int>();
            
            for (int i = 0; i < blockLimit; i++)
            {
                DataBlock.Append<int, int>(db, i, i);
            }
            
            db.RowCount.ShouldEqual(blockLimit);
            
            var dbBeforeExpand = db;
            
            DataBlock.Append<int, int>(db, blockLimit, blockLimit);
            
            db.RowCount.ShouldEqual(2);
            db.Values.RuntimeTypeId.ShouldEqual(VecTypeHelper<DataBlock>.RuntimeTypeId);
            db.Values.UnsafeReadUnaligned<DataBlock>(0).ShouldNotEqual(dbBeforeExpand);

        }
    }
}