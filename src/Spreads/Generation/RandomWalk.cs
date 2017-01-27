// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
#if NET451
using MathNet.Numerics.Distributions;
#endif
using Spreads.Collections;
using System;
using Angara;

namespace Spreads.Generation {

    public static class RandomWalk {

        public static SortedMap<DateTime, double> Generate(DateTime startDate, DateTime endDate, TimeSpan step,
            double annualizedVolatility, double drift = 0.0, double daysPerYear = 365.25, int? seed = null) {
            if (startDate > endDate) throw new ArgumentException();
            var capacity = (int)((endDate - startDate).Ticks / step.Ticks);
            var current = 0.0;
            var sm = new SortedMap<DateTime, double>(capacity) { { startDate, current } };
            var tDelta = step.Ticks / (daysPerYear * TimeSpan.TicksPerDay);
            var driftPerStep = drift * tDelta;
            var dt = startDate + step;
            seed = seed ?? (new System.Random()).Next(int.MinValue, int.MaxValue);
#if NET451
            var gen = new MathNet.Numerics.Random.MersenneTwister(seed.Value, false);
            var nd = new Normal(0.0, annualizedVolatility * Math.Sqrt(tDelta));
#else
            var nd2 = new Statistics.Distribution.Normal(0.0, annualizedVolatility * Math.Sqrt(tDelta));
            var gen = new Statistics.MT19937(unchecked((uint)seed.Value));
#endif
            
            
            while (dt < endDate) {
#if NET451
                var random = nd.Sample();
#else
                var random =  Statistics.draw(gen, nd2);
#endif
                current += driftPerStep + random;
                dt = dt + step;
                sm.Add(dt, current);
            }
            return sm;
        }
    }
}
