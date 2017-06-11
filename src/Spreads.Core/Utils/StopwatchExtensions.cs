// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;

namespace Spreads.Utils
{
    internal static class StopwatchExtensions
    {
        [Obsolete("Use Benchmark utility")]
        public static double MOPS(this Stopwatch stopwatch, int count, int decimals = 2)
        {
            return Math.Round((count * 0.001) / ((double)stopwatch.ElapsedMilliseconds), decimals);
        }
    }
}