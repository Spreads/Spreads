// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.DataTypes;

namespace Spreads.Core.Tests
{
   
    [TestFixture]
    public class DataSteamTests
    {
        [Test, Ignore("TODO")]
        public void ImplicitConversion()
        {
            var ds1 = new DataStream<double>();
            var ds2 = new Series<ulong, Timestamped<double>, DataStreamCursor<double>>();
            Series<ulong, Timestamped<double>> ds3 = default;

            // Does not work, there is no info to what cast ds
            //var x0 = ds1 + ds1;

            // works
            var x1 = ds1 + ds2;

            // var x2 = ds1 + ds3;

            // var x3 = ds1 + (ds2 + ds2);
        }
    }
}
