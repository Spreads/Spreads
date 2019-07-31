// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Algorithms.Online;
using Spreads.Collections;
using System;

namespace Spreads.Tests.Algorithms.Online {

    [TestFixture]
    public class MovingRegressionTest {

        //[Test]
        //public void CouldCalculateOnlineMovingRegression() {
        //    var rng = new Random();
        //    var y = new SortedMap<DateTime, double>();
        //    var xx = new SortedMap<DateTime, double[]>();
        //    var dt = DateTime.Today;
        //    for (int i = 0; i < 10000; i++) {
        //        var xrow = new double[3];
        //        xrow[0] = 1;
        //        xrow[1] = rng.NextDouble() * 5 * i;
        //        xrow[2] = rng.NextDouble() * 10 * i;
        //        var yrow = 0.33 + 0.25 * xrow[1] + 1.5 * xrow[2] + (rng.NextDouble() - 0.5);
        //        y.Add(dt, yrow);
        //        xx.Add(dt, xrow);
        //        dt = dt.AddSeconds(1);
        //    }

        //    var movingRegression = y.MovingRegression(xx, 1000, 100);
        //    foreach (var kvp in movingRegression) {
        //        Console.WriteLine($"{kvp.Value[0, 0]} - {kvp.Value[1, 0]} - {kvp.Value[2, 0]}");
        //    }
        //}
    }
}