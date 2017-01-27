// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using MathNet.Numerics.Distributions;
using Spreads.Collections;
using System;

namespace Spreads.Generation {

    public static class RandomWalk {

        public static SortedMap<DateTime, double> Generate(DateTime startDate, DateTime endDate, TimeSpan step,
            double annualizedVolatility, double drift = 0.0, double daysPerYear = 365.25) {
            if (startDate > endDate) throw new ArgumentException();
            var capacity = (int)((endDate - startDate).Ticks / step.Ticks);
            var current = 0.0;
            var sm = new SortedMap<DateTime, double>(capacity) { { startDate, current } };
            var tDelta = step.Ticks / (daysPerYear * TimeSpan.TicksPerDay);
            var driftPerStep = drift * tDelta;
            var dt = startDate + step;
            var nd = new Normal(0.0, annualizedVolatility * Math.Sqrt(tDelta));
            while (dt < endDate) {
                current += driftPerStep + nd.Sample();
                dt = dt + step;
                sm.Add(dt, current);
            }
            return sm;
        }
    }
}
